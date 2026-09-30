using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace PersonalAppsHub.Services;

public sealed record GameFrameRateMetrics(int ProcessId, double AverageFps, double OnePercentLowFps,
    double AverageFrameTimeMs, long FrameCount);

public sealed class GameFrameRateService : IDisposable
{
    private sealed class CaptureState
    {
        public required Process Process { get; init; }
        public int FrameTimeColumn { get; set; } = -1;
        public double FrameTimeTotal { get; set; }
        public long FrameCount { get; set; }
        public List<double> FrameTimes { get; } = [];
        public object Sync { get; } = new();
    }

    private readonly Dictionary<int, CaptureState> _captures = [];
    private readonly string _presentMonPath = Path.Combine(AppContext.BaseDirectory, "Tools", "PresentMon.exe");

    public IReadOnlyDictionary<int, GameFrameRateMetrics> Capture(IReadOnlyList<ActiveGameSession> sessions)
    {
        var activeIds = sessions.Select(session => session.ProcessId).ToHashSet();
        foreach (var session in sessions)
            if (!_captures.ContainsKey(session.ProcessId)) StartCapture(session.ProcessId);

        var result = new Dictionary<int, GameFrameRateMetrics>();
        foreach (var entry in _captures.ToArray())
        {
            lock (entry.Value.Sync)
                if (entry.Value.FrameCount > 0)
                    result[entry.Key] = BuildMetrics(entry.Key, entry.Value);
            if (!activeIds.Contains(entry.Key)) StopCapture(entry.Key, entry.Value);
        }
        return result;
    }

    public bool IsAvailable => File.Exists(_presentMonPath);

    private void StartCapture(int processId)
    {
        if (!IsAvailable) return;
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _presentMonPath,
                    // Une fermeture forcée de FlexHub peut laisser la session ETW précédente active.
                    // PresentMon la remplace alors avant de reprendre la mesure du même processus.
                    Arguments = $"-process_id {processId} -output_stdout -session_name FlexHub_{processId} -stop_existing_session -terminate_on_proc_exit -no_top",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };
            var state = new CaptureState { Process = process };
            process.OutputDataReceived += (_, args) => ParseFrameLine(state, args.Data);
            process.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data)) AppLog.Write($"PresentMon {processId} : {args.Data}");
            };
            process.Exited += (_, _) =>
            {
                lock (state.Sync)
                    if (state.FrameCount == 0) AppLog.Write($"PresentMon {processId} s’est arrêté sans fournir d’image (code {SafeExitCode(process)}). ");
            };
            if (!process.Start()) { process.Dispose(); return; }
            _captures[processId] = state;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            AppLog.Write($"Mesure FPS démarrée pour le processus {processId}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            AppLog.Write($"Mesure FPS indisponible pour le processus {processId} : {ex.Message}");
        }
    }

    private static void ParseFrameLine(CaptureState state, string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        lock (state.Sync)
        {
            if (state.FrameTimeColumn < 0)
            {
                var headers = line.TrimStart('\uFEFF').Split(',');
                state.FrameTimeColumn = Array.FindIndex(headers, header =>
                    header.Equals("msBetweenPresents", StringComparison.OrdinalIgnoreCase) ||
                    header.Equals("MsBetweenPresents", StringComparison.OrdinalIgnoreCase) ||
                    header.Equals("FrameTime", StringComparison.OrdinalIgnoreCase));
                return;
            }
            var columns = line.Split(',');
            if (state.FrameTimeColumn >= columns.Length ||
                !double.TryParse(columns[state.FrameTimeColumn], NumberStyles.Float, CultureInfo.InvariantCulture, out var frameTime) ||
                frameTime is < 0.1 or > 1000) return;
            state.FrameTimeTotal += frameTime;
            state.FrameCount++;
            state.FrameTimes.Add(frameTime);
        }
    }

    private static GameFrameRateMetrics BuildMetrics(int processId, CaptureState state)
    {
        var averageFrameTime = state.FrameTimeTotal / state.FrameCount;
        var slowFrameCount = Math.Max(1, (int)Math.Ceiling(state.FrameTimes.Count * 0.01));
        var slowFrameAverage = state.FrameTimes.OrderByDescending(value => value).Take(slowFrameCount).Average();
        return new GameFrameRateMetrics(processId, 1000d / averageFrameTime, 1000d / slowFrameAverage,
            averageFrameTime, state.FrameCount);
    }

    private void StopCapture(int processId, CaptureState state)
    {
        try { if (!state.Process.HasExited) state.Process.Kill(true); } catch { }
        state.Process.Dispose();
        _captures.Remove(processId);
    }

    private static int SafeExitCode(Process process)
    {
        try { return process.ExitCode; } catch { return -1; }
    }

    public void Dispose()
    {
        foreach (var entry in _captures.ToArray()) StopCapture(entry.Key, entry.Value);
    }
}
