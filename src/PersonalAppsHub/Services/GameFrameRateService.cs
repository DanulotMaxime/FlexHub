using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace PersonalAppsHub.Services;

public sealed record GameFrameRateMetrics(int ProcessId, double AverageFps, double OnePercentLowFps,
    double AverageFrameTimeMs, long FrameCount);

public sealed class GameFrameRateService : IDisposable
{
    private sealed class FrameState
    {
        public double FrameTimeTotal { get; set; }
        public long FrameCount { get; set; }
        public List<double> FrameTimes { get; } = [];
    }

    private readonly object _sync = new();
    private readonly Dictionary<int, FrameState> _framesByProcess = [];
    private readonly string _presentMonPath = Path.Combine(AppContext.BaseDirectory, "Tools", "PresentMon.exe");
    private HashSet<int> _activeProcessIds = [];
    private Process? _captureProcess;
    private int _processIdColumn = -1;
    private int _frameTimeColumn = -1;

    public IReadOnlyDictionary<int, GameFrameRateMetrics> Capture(IReadOnlyList<ActiveGameSession> sessions)
    {
        var activeIds = sessions.Select(session => session.ProcessId).ToHashSet();
        lock (_sync)
        {
            _activeProcessIds = activeIds;
            foreach (var obsoleteId in _framesByProcess.Keys.Where(id => !activeIds.Contains(id)).ToArray())
                _framesByProcess.Remove(obsoleteId);
        }

        if (activeIds.Count == 0)
        {
            StopCapture();
            return new Dictionary<int, GameFrameRateMetrics>();
        }

        if (!CaptureIsRunning())
        {
            StopCapture();
            StartCapture();
        }

        lock (_sync)
            return _framesByProcess
                .Where(entry => activeIds.Contains(entry.Key) && entry.Value.FrameCount > 0)
                .ToDictionary(entry => entry.Key, entry => BuildMetrics(entry.Key, entry.Value));
    }

    public bool IsAvailable => File.Exists(_presentMonPath);

    private bool CaptureIsRunning()
    {
        try { return _captureProcess is { HasExited: false }; }
        catch (InvalidOperationException) { return false; }
    }

    private void StartCapture()
    {
        if (!IsAvailable) return;
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _presentMonPath,
                    // Une seule session ETW collecte toutes les images. Démarrer une session par jeu
                    // finit par épuiser les ressources ETW de Windows (erreur PresentMon 1450).
                    Arguments = "-captureall -output_stdout -session_name FlexHub -stop_existing_session -no_top",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };
            process.OutputDataReceived += (_, args) => ParseFrameLine(args.Data);
            process.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data)) AppLog.Write($"PresentMon : {args.Data}");
            };
            process.Exited += (_, _) =>
            {
                long frameCount;
                lock (_sync) frameCount = _framesByProcess.Values.Sum(state => state.FrameCount);
                if (frameCount == 0)
                    AppLog.Write($"PresentMon s’est arrêté sans fournir d’image (code {SafeExitCode(process)}).");
            };
            if (!process.Start()) { process.Dispose(); return; }
            _captureProcess = process;
            lock (_sync)
            {
                _processIdColumn = -1;
                _frameTimeColumn = -1;
            }
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            AppLog.Write("Mesure FPS démarrée avec une session PresentMon unique.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
        {
            AppLog.Write($"Mesure FPS indisponible : {ex.Message}");
        }
    }

    private void ParseFrameLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        lock (_sync)
        {
            var columns = line.TrimStart('\uFEFF').Split(',');
            if (_processIdColumn < 0 || _frameTimeColumn < 0)
            {
                _processIdColumn = Array.FindIndex(columns, header =>
                    header.Equals("ProcessID", StringComparison.OrdinalIgnoreCase) ||
                    header.Equals("ProcessId", StringComparison.OrdinalIgnoreCase));
                _frameTimeColumn = Array.FindIndex(columns, header =>
                    header.Equals("msBetweenPresents", StringComparison.OrdinalIgnoreCase) ||
                    header.Equals("FrameTime", StringComparison.OrdinalIgnoreCase));
                return;
            }

            if (_processIdColumn >= columns.Length || _frameTimeColumn >= columns.Length ||
                !int.TryParse(columns[_processIdColumn], NumberStyles.Integer, CultureInfo.InvariantCulture, out var processId) ||
                !_activeProcessIds.Contains(processId) ||
                !double.TryParse(columns[_frameTimeColumn], NumberStyles.Float, CultureInfo.InvariantCulture, out var frameTime) ||
                frameTime is < 0.1 or > 1000) return;

            if (!_framesByProcess.TryGetValue(processId, out var state))
            {
                state = new FrameState();
                _framesByProcess[processId] = state;
            }
            state.FrameTimeTotal += frameTime;
            state.FrameCount++;
            state.FrameTimes.Add(frameTime);
        }
    }

    private static GameFrameRateMetrics BuildMetrics(int processId, FrameState state)
    {
        var averageFrameTime = state.FrameTimeTotal / state.FrameCount;
        var slowFrameCount = Math.Max(1, (int)Math.Ceiling(state.FrameTimes.Count * 0.01));
        var slowFrameAverage = state.FrameTimes.OrderByDescending(value => value).Take(slowFrameCount).Average();
        return new GameFrameRateMetrics(processId, 1000d / averageFrameTime, 1000d / slowFrameAverage,
            averageFrameTime, state.FrameCount);
    }

    private void StopCapture()
    {
        var process = _captureProcess;
        _captureProcess = null;
        if (process is null) return;
        try { if (!process.HasExited) process.Kill(true); } catch { }
        process.Dispose();
    }

    private static int SafeExitCode(Process process)
    {
        try { return process.ExitCode; } catch { return -1; }
    }

    public void Dispose() => StopCapture();
}
