using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace PersonalAppsHub.Services;

public sealed record SystemMetrics(double CpuPercent, double RamPercent, double RamUsedGb, double RamTotalGb,
    double? GpuPercent, double? VramPercent, double? VramUsedGb, double? VramTotalGb,
    double? GpuTemperatureC, double? CpuPowerWatts, double? GpuPowerWatts, double? GpuPowerLimitWatts,
    string? TopCpuProcessText, string? TopRamProcessText, DateTime CapturedAt)
{
    public double? TotalMeasuredPowerWatts => CpuPowerWatts.HasValue && GpuPowerWatts.HasValue
        ? CpuPowerWatts.Value + GpuPowerWatts.Value
        : null;
}

public sealed class SystemMonitoringService : IDisposable
{
    private const string RyzenMasterCliPath = @"C:\Program Files\AMD\RyzenMasterSDK\AMDRyzenMasterCLI\bin-prebuilt\AMDRyzenMasterCLI.exe";
    private static readonly TimeSpan CpuPowerRefreshInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan RyzenMasterTimeout = TimeSpan.FromSeconds(8);
    private readonly Computer _hardware = new() { IsCpuEnabled = true, IsGpuEnabled = true };
    private bool _hardwareOpened;
    private ulong? _previousIdle;
    private ulong? _previousTotal;
    private double? _cachedCpuPowerWatts;
    private DateTime _lastCpuPowerReadUtc = DateTime.MinValue;
    private readonly Dictionary<int, TimeSpan> _previousProcessCpu = new();
    private DateTime? _previousProcessSampleUtc;

    public async Task<SystemMetrics> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var cpu = ReadCpuPercent();
        var (ramPercent, ramUsed, ramTotal) = ReadMemory();
        var processesTask = Task.Run(ReadTopProcesses, cancellationToken);
        var gpu = await ReadNvidiaMetricsAsync(cancellationToken) ?? ReadHardwareGpuMetrics();
        if (DateTime.UtcNow - _lastCpuPowerReadUtc >= CpuPowerRefreshInterval)
        {
            _cachedCpuPowerWatts = await ReadCpuPowerAsync(cancellationToken);
            _lastCpuPowerReadUtc = DateTime.UtcNow;
        }
        var processes = await processesTask;
        return new SystemMetrics(cpu, ramPercent, ramUsed, ramTotal, gpu?.Usage,
            gpu?.VramPercent, gpu?.VramUsedGb, gpu?.VramTotalGb, gpu?.Temperature,
            _cachedCpuPowerWatts, gpu?.PowerWatts, gpu?.PowerLimitWatts,
            processes.TopCpu, processes.TopRam, DateTime.Now);
    }

    private async Task<double?> ReadCpuPowerAsync(CancellationToken cancellationToken)
    {
        var amdPower = await ReadAmdRyzenPowerAsync(cancellationToken);
        return amdPower ?? await Task.Run(ReadCpuPowerFromHardwareSensors, cancellationToken);
    }

    private static async Task<double?> ReadAmdRyzenPowerAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(RyzenMasterCliPath)) return null;

        Process? process = null;
        try
        {
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = RyzenMasterCliPath,
                    Arguments = "-a GetPMTableData",
                    WorkingDirectory = Path.GetDirectoryName(RyzenMasterCliPath)!,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.Start();
            try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RyzenMasterTimeout);
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var output = await outputTask;
            _ = await errorTask;

            if (process.ExitCode != 0) return null;
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                const string marker = "PPT Current Value :";
                var markerIndex = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex < 0) continue;
                var valueText = line[(markerIndex + marker.Length)..]
                    .Replace("W", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                if (double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var watts) && watts > 0.5)
                    return watts;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            AppLog.Write("Lecture AMD Ryzen Master interrompue après 8 secondes.");
        }
        catch (Exception ex)
        {
            AppLog.Write($"Lecture AMD Ryzen Master indisponible : {ex.Message}");
        }
        finally
        {
            if (process is not null)
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
            }
            process?.Dispose();
        }
        return null;
    }

    private double? ReadCpuPowerFromHardwareSensors()
    {
        try
        {
            EnsureHardwareOpen();
            foreach (var hardware in _hardware.Hardware.Where(item => item.HardwareType == HardwareType.Cpu))
            {
                hardware.Update();
                var sensors = hardware.Sensors.Where(sensor => sensor.SensorType == SensorType.Power && sensor.Value.HasValue).ToArray();
                if (sensors.Length == 0) continue;
                var package = sensors.FirstOrDefault(sensor => sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))?.Value;
                var cores = sensors.FirstOrDefault(sensor => sensor.Name.Contains("Cores", StringComparison.OrdinalIgnoreCase))?.Value;
                var best = package ?? cores ?? sensors.Max(sensor => (float?)sensor.Value);
                // Certains contrôleurs Ryzen publient un capteur présent mais bloqué à 0 W.
                // Une telle valeur ne doit pas être présentée comme une mesure réelle.
                if (best is > 0.5f) return best.Value;
            }
        }
        catch (Exception ex) { AppLog.Write($"Capteur de puissance CPU indisponible : {ex.Message}"); }
        return null;
    }

    private (string? TopCpu, string? TopRam) ReadTopProcesses()
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = _previousProcessSampleUtc.HasValue ? (now - _previousProcessSampleUtc.Value).TotalSeconds : 0;
        var currentCpu = new Dictionary<int, TimeSpan>();
        (string Name, double Percent)? topCpu = null;
        (string Name, long Bytes)? topRam = null;
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    var name = process.ProcessName;
                    var cpuTime = process.TotalProcessorTime;
                    var memory = process.WorkingSet64;
                    currentCpu[process.Id] = cpuTime;
                    if (elapsedSeconds > 0 && _previousProcessCpu.TryGetValue(process.Id, out var previousCpu))
                    {
                        var percent = Math.Clamp((cpuTime - previousCpu).TotalSeconds /
                            (elapsedSeconds * Environment.ProcessorCount) * 100d, 0, 100);
                        if (topCpu is null || percent > topCpu.Value.Percent) topCpu = (name, percent);
                    }
                    if (topRam is null || memory > topRam.Value.Bytes) topRam = (name, memory);
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            }
        }
        _previousProcessCpu.Clear();
        foreach (var sample in currentCpu) _previousProcessCpu[sample.Key] = sample.Value;
        _previousProcessSampleUtc = now;
        return (topCpu is { Percent: >= 0.1 } ? $"{topCpu.Value.Name} · {topCpu.Value.Percent:0.0}%" : null,
            topRam is not null ? $"{topRam.Value.Name} · {topRam.Value.Bytes / 1024d / 1024d:0} Mo" : null);
    }

    private NvidiaMetrics? ReadHardwareGpuMetrics()
    {
        try
        {
            EnsureHardwareOpen();
            foreach (var hardware in _hardware.Hardware.Where(item =>
                         item.HardwareType is HardwareType.GpuAmd or HardwareType.GpuNvidia or HardwareType.GpuIntel))
            {
                hardware.Update();
                var sensors = hardware.Sensors.Where(sensor => sensor.Value.HasValue).ToArray();
                var usage = PreferredSensorValue(sensors, SensorType.Load, "GPU Core") ??
                            sensors.Where(sensor => sensor.SensorType == SensorType.Load).Max(sensor => (double?)sensor.Value);
                var temperature = PreferredSensorValue(sensors, SensorType.Temperature, "GPU Core") ??
                                  sensors.Where(sensor => sensor.SensorType == SensorType.Temperature).Max(sensor => (double?)sensor.Value);
                var usedMb = sensors.FirstOrDefault(sensor => sensor.Name.Contains("Memory Used", StringComparison.OrdinalIgnoreCase))?.Value;
                var totalMb = sensors.FirstOrDefault(sensor => sensor.Name.Contains("Memory Total", StringComparison.OrdinalIgnoreCase))?.Value;
                var powerSensors = sensors.Where(sensor => sensor.SensorType == SensorType.Power).ToArray();
                var power = powerSensors.FirstOrDefault(sensor =>
                    (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) || sensor.Name.Contains("GPU", StringComparison.OrdinalIgnoreCase)))?.Value
                    ?? powerSensors.Max(sensor => (float?)sensor.Value);
                var vramPercent = usedMb.HasValue && totalMb > 0 ? usedMb.Value * 100d / totalMb.Value : (double?)null;
                if (usage.HasValue || temperature.HasValue)
                    return new NvidiaMetrics(usage ?? 0, vramPercent, usedMb / 1024d, totalMb / 1024d, temperature, power, null);
            }
        }
        catch (Exception ex) { AppLog.Write($"Capteurs GPU indisponibles : {ex.Message}"); }
        return null;
    }

    private void EnsureHardwareOpen()
    {
        if (_hardwareOpened) return;
        _hardware.Open();
        _hardwareOpened = true;
    }

    private static double? PreferredSensorValue(IEnumerable<ISensor> sensors, SensorType type, string name) =>
        sensors.FirstOrDefault(sensor => sensor.SensorType == type &&
            sensor.Name.Contains(name, StringComparison.OrdinalIgnoreCase))?.Value;

    public void Dispose()
    {
        if (_hardwareOpened) _hardware.Close();
    }

    public static double Percentage(ulong used, ulong total) =>
        total == 0 ? 0 : Math.Clamp(used * 100d / total, 0, 100);

    private double ReadCpuPercent()
    {
        if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime)) return 0;
        var idle = ToUInt64(idleTime);
        var total = ToUInt64(kernelTime) + ToUInt64(userTime);
        var elapsed = _previousTotal.HasValue ? total - _previousTotal.Value : 0;
        var idleElapsed = _previousIdle.HasValue ? idle - _previousIdle.Value : 0;
        var result = elapsed > 0 ? Percentage(elapsed - Math.Min(elapsed, idleElapsed), elapsed) : 0;
        _previousIdle = idle;
        _previousTotal = total;
        return result;
    }

    private static (double Percent, double UsedGb, double TotalGb) ReadMemory()
    {
        var status = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(status)) return (0, 0, 0);
        var used = status.TotalPhysical - status.AvailablePhysical;
        const double gb = 1024d * 1024d * 1024d;
        return (Percentage(used, status.TotalPhysical), used / gb, status.TotalPhysical / gb);
    }

    private static async Task<NvidiaMetrics?> ReadNvidiaMetricsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process { StartInfo = new ProcessStartInfo
            {
                FileName = "nvidia-smi.exe",
                Arguments = "--query-gpu=utilization.gpu,memory.used,memory.total,temperature.gpu,power.draw,power.limit --format=csv,noheader,nounits",
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            }};
            process.Start();
            var lineTask = process.StandardOutput.ReadLineAsync(cancellationToken).AsTask();
            await process.WaitForExitAsync(cancellationToken);
            var line = await lineTask;
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(line)) return null;
            var values = line.Split(',', StringSplitOptions.TrimEntries);
            if (values.Length < 4 || !TryNumber(values[0], out var usage) || !TryNumber(values[1], out var usedMb) ||
                !TryNumber(values[2], out var totalMb) || !TryNumber(values[3], out var temperature)) return null;
            var power = values.Length > 4 && TryNumber(values[4], out var parsedPower) ? parsedPower : (double?)null;
            var powerLimit = values.Length > 5 && TryNumber(values[5], out var parsedLimit) ? parsedLimit : (double?)null;
            return new NvidiaMetrics(usage, Percentage((ulong)Math.Max(0, usedMb), (ulong)Math.Max(0, totalMb)),
                usedMb / 1024d, totalMb / 1024d, temperature, power, powerLimit);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return null; }
    }

    private static bool TryNumber(string value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    private static ulong ToUInt64(FileTime time) => ((ulong)time.High << 32) | time.Low;
    private sealed record NvidiaMetrics(double Usage, double? VramPercent, double? VramUsedGb, double? VramTotalGb,
        double? Temperature, double? PowerWatts, double? PowerLimitWatts);

    [StructLayout(LayoutKind.Sequential)] private struct FileTime { public uint Low; public uint High; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>(); public uint MemoryLoad;
        public ulong TotalPhysical; public ulong AvailablePhysical; public ulong TotalPageFile;
        public ulong AvailablePageFile; public ulong TotalVirtual; public ulong AvailableVirtual; public ulong AvailableExtendedVirtual;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);
}
