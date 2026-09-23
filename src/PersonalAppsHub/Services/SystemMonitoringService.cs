using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace PersonalAppsHub.Services;

public sealed record SystemMetrics(double CpuPercent, double? CpuTemperatureC, double RamPercent, double RamUsedGb, double RamTotalGb,
    double? GpuPercent, double? VramPercent, double? VramUsedGb, double? VramTotalGb,
    double? GpuTemperatureC, DateTime CapturedAt);

public sealed class SystemMonitoringService : IDisposable
{
    private readonly Computer _hardware = new() { IsCpuEnabled = true, IsGpuEnabled = true };
    private bool _hardwareOpened;
    private ulong? _previousIdle;
    private ulong? _previousTotal;

    public async Task<SystemMetrics> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var cpu = ReadCpuPercent();
        var (ramPercent, ramUsed, ramTotal) = ReadMemory();
        var gpu = await ReadNvidiaMetricsAsync(cancellationToken) ?? ReadHardwareGpuMetrics();
        var cpuTemperature = ReadCpuTemperature();
        return new SystemMetrics(cpu, cpuTemperature, ramPercent, ramUsed, ramTotal, gpu?.Usage,
            gpu?.VramPercent, gpu?.VramUsedGb, gpu?.VramTotalGb, gpu?.Temperature, DateTime.Now);
    }

    private double? ReadCpuTemperature()
    {
        try
        {
            EnsureHardwareOpen();
            var temperatures = new List<double>();
            foreach (var hardware in _hardware.Hardware.Where(item => item.HardwareType == HardwareType.Cpu))
            {
                hardware.Update();
                temperatures.AddRange(hardware.Sensors
                    .Where(sensor => sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue)
                    .Select(sensor => (double)sensor.Value!.Value));
            }
            return temperatures.Count == 0 ? null : temperatures.Max();
        }
        catch (Exception ex)
        {
            AppLog.Write($"Température CPU indisponible : {ex.Message}");
            return null;
        }
    }

    private NvidiaMetrics? ReadHardwareGpuMetrics()
    {
        try
        {
            EnsureHardwareOpen();
            foreach (var hardware in _hardware.Hardware.Where(item =>
                         item.HardwareType is HardwareType.GpuAmd or HardwareType.GpuNvidia))
            {
                hardware.Update();
                var sensors = hardware.Sensors.Where(sensor => sensor.Value.HasValue).ToArray();
                var usage = PreferredSensorValue(sensors, SensorType.Load, "GPU Core") ??
                            sensors.Where(sensor => sensor.SensorType == SensorType.Load).Max(sensor => (double?)sensor.Value);
                var temperature = PreferredSensorValue(sensors, SensorType.Temperature, "GPU Core") ??
                                  sensors.Where(sensor => sensor.SensorType == SensorType.Temperature).Max(sensor => (double?)sensor.Value);
                var usedMb = sensors.FirstOrDefault(sensor => sensor.Name.Contains("Memory Used", StringComparison.OrdinalIgnoreCase))?.Value;
                var totalMb = sensors.FirstOrDefault(sensor => sensor.Name.Contains("Memory Total", StringComparison.OrdinalIgnoreCase))?.Value;
                var vramPercent = usedMb.HasValue && totalMb > 0 ? usedMb.Value * 100d / totalMb.Value : (double?)null;
                if (usage.HasValue || temperature.HasValue)
                    return new NvidiaMetrics(usage ?? 0, vramPercent, usedMb / 1024d, totalMb / 1024d, temperature);
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
                Arguments = "--query-gpu=utilization.gpu,memory.used,memory.total,temperature.gpu --format=csv,noheader,nounits",
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
            return new NvidiaMetrics(usage, Percentage((ulong)Math.Max(0, usedMb), (ulong)Math.Max(0, totalMb)),
                usedMb / 1024d, totalMb / 1024d, temperature);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return null; }
    }

    private static bool TryNumber(string value, out double result) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    private static ulong ToUInt64(FileTime time) => ((ulong)time.High << 32) | time.Low;
    private sealed record NvidiaMetrics(double Usage, double? VramPercent, double? VramUsedGb, double? VramTotalGb, double? Temperature);

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
