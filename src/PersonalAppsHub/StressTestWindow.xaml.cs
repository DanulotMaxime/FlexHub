using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using PersonalAppsHub.Services;

namespace PersonalAppsHub;

public partial class StressTestWindow : Window
{
    private const double GpuSafetyTemperatureC = 90;
    private readonly SystemMonitoringService _monitor = new();
    private readonly StressTestHistoryService _history = new();
    private readonly Direct3DGpuLoadService _gpuLoad = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _animation = new();
    private readonly List<Ellipse> _shapes = [];
    private CancellationTokenSource? _cancellation;
    private DateTime _startedAt, _endsAt;
    private double _phase, _cpuTotal, _cpuPeak, _gpuTotal, _gpuPeak;
    private int _sampleCount, _gpuSampleCount, _targetLoad = 60;
    private double? _gpuTemperaturePeak;
    private bool _safetyStop;
    private bool _cpuTestEnabled, _gpuTestEnabled;
    private string _level = "Moyen";

    public StressTestWindow()
    {
        InitializeComponent();
        HistoryList.ItemsSource = _history.GetRecent();
        StartButton.Click += async (_, _) => await StartAsync();
        StopButton.Click += (_, _) => Stop("Test arrêté par l’utilisateur.");
        CloseTopButton.Click += (_, _) => Close();
        _timer.Tick += async (_, _) => await RefreshMetricsAsync();
        _animation.Tick += (_, _) => AnimateGpuLoad();
        PreviewKeyDown += (_, args) => { if (args.Key == System.Windows.Input.Key.Escape) Close(); };
        Closed += (_, _) => { Stop(null); _gpuLoad.Dispose(); _monitor.Dispose(); };
        Loaded += (_, _) => CreateGpuShapes();
    }

    private async Task StartAsync()
    {
        var testType = ((ComboBoxItem)TestTypeBox.SelectedItem).Tag.ToString();
        _cpuTestEnabled = testType is "CPU" or "BOTH";
        _gpuTestEnabled = testType is "GPU" or "BOTH";
        var levelItem = (ComboBoxItem)LevelBox.SelectedItem;
        _level = levelItem.Content.ToString()!;
        _targetLoad = int.Parse(levelItem.Tag.ToString()!);
        var seconds = int.Parse(((ComboBoxItem)DurationBox.SelectedItem).Tag.ToString()!);
        if (_targetLoad == 100 && seconds > 60)
        {
            seconds = 60;
            StatusText.Text = "Le niveau Extrême est limité automatiquement à 60 secondes.";
        }
        ResetMetrics();
        _cancellation = new CancellationTokenSource();
        _startedAt = DateTime.Now;
        _endsAt = _startedAt.AddSeconds(seconds);
        SetControls(false);
        AppLog.Write($"TEST DE CHARGE DÉMARRÉ | Niveau={_level} | Durée={seconds}s | CPU={_cpuTestEnabled} | GPU={_gpuTestEnabled}");
        var token = _cancellation.Token;
        if (_cpuTestEnabled)
            for (var index = 0; index < Environment.ProcessorCount; index++)
                _ = Task.Run(() => RunCpuWorker(token, _targetLoad), token);
        if (_gpuTestEnabled)
        {
            _gpuLoad.Start(_targetLoad);
            var fps = _targetLoad switch { <= 50 => 45, <= 75 => 60, <= 90 => 90, _ => 120 };
            _animation.Interval = TimeSpan.FromMilliseconds(1000d / fps);
            _animation.Start();
        }
        _timer.Start();
        await RefreshMetricsAsync();
    }

    private static void RunCpuWorker(CancellationToken token, int targetLoad)
    {
        var stopwatch = new Stopwatch();
        while (!token.IsCancellationRequested)
        {
            stopwatch.Restart();
            while (stopwatch.ElapsedMilliseconds < targetLoad && !token.IsCancellationRequested)
                Thread.SpinWait(20_000);
            if (token.WaitHandle.WaitOne(100 - targetLoad)) return;
        }
    }

    private async Task RefreshMetricsAsync()
    {
        if (_cancellation is null) return;
        if (DateTime.Now >= _endsAt) { Stop("Test terminé normalement."); return; }
        try
        {
            if (_gpuTestEnabled && _gpuLoad.LastError is not null)
            {
                Stop($"Charge Direct3D indisponible : {_gpuLoad.LastError}");
                return;
            }
            var metrics = await _monitor.CaptureAsync(_cancellation.Token);
            _sampleCount++;
            _cpuTotal += metrics.CpuPercent;
            _cpuPeak = Math.Max(_cpuPeak, metrics.CpuPercent);
            if (metrics.GpuPercent.HasValue)
            {
                _gpuSampleCount++;
                _gpuTotal += metrics.GpuPercent.Value;
                _gpuPeak = Math.Max(_gpuPeak, metrics.GpuPercent.Value);
            }
            if (metrics.GpuTemperatureC.HasValue)
                _gpuTemperaturePeak = Math.Max(_gpuTemperaturePeak ?? double.MinValue, metrics.GpuTemperatureC.Value);
            MetricsText.Text = $"CPU {metrics.CpuPercent:0}%  ·  GPU {(metrics.GpuPercent.HasValue ? $"{metrics.GpuPercent:0}%" : "—")}  ·  Température GPU {(metrics.GpuTemperatureC.HasValue ? $"{metrics.GpuTemperatureC:0} °C" : "—")}";
            CountdownText.Text = $"{Math.Max(0, Math.Ceiling((_endsAt - DateTime.Now).TotalSeconds)):0} s";
            if (metrics.GpuTemperatureC >= GpuSafetyTemperatureC)
            {
                _safetyStop = true;
                Stop($"Arrêt de sécurité : le GPU a atteint {metrics.GpuTemperatureC:0} °C.");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Stop($"Test interrompu : {ex.Message}"); }
    }

    private void CreateGpuShapes()
    {
        for (var index = 0; index < 260; index++)
        {
            var shape = new Ellipse { Width = 20 + index % 22, Height = 20 + index % 22,
                Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(155, (byte)(80 + index % 170), (byte)(90 + index * 7 % 150), (byte)(120 + index * 11 % 130))) };
            _shapes.Add(shape);
            GpuCanvas.Children.Add(shape);
        }
    }

    private void AnimateGpuLoad()
    {
        _phase += 0.03 + _targetLoad / 1800d;
        var activeShapes = _targetLoad switch { <= 50 => 100, <= 75 => 170, <= 90 => 230, _ => 260 };
        var width = Math.Max(1, GpuCanvas.ActualWidth - 45);
        var height = Math.Max(1, GpuCanvas.ActualHeight - 45);
        for (var index = 0; index < _shapes.Count; index++)
        {
            _shapes[index].Visibility = index < activeShapes ? Visibility.Visible : Visibility.Collapsed;
            if (index >= activeShapes) continue;
            Canvas.SetLeft(_shapes[index], (Math.Sin(_phase * (1 + index % 7 * .05) + index) + 1) * width / 2);
            Canvas.SetTop(_shapes[index], (Math.Cos(_phase * (1.2 + index % 5 * .07) + index * .7) + 1) * height / 2);
        }
    }

    private void Stop(string? message)
    {
        var wasRunning = _cancellation is not null;
        _cancellation?.Cancel();
        _cancellation = null;
        _timer.Stop();
        _animation.Stop();
        _gpuLoad.Stop();
        SetControls(true);
        if (message is not null) StatusText.Text = message;
        CountdownText.Text = "Prêt";
        if (!wasRunning || _sampleCount == 0) return;

        var duration = Math.Max(1, (int)Math.Round((DateTime.Now - _startedAt).TotalSeconds));
        var analysis = BuildAnalysis();
        var result = new StressTestResult
        {
            StartedAt = _startedAt, DurationSeconds = duration, Level = _level,
            CpuEnabled = _cpuTestEnabled, GpuEnabled = _gpuTestEnabled,
            AverageCpuPercent = _cpuTotal / _sampleCount, PeakCpuPercent = _cpuPeak,
            AverageGpuPercent = _gpuSampleCount > 0 ? _gpuTotal / _gpuSampleCount : null,
            PeakGpuPercent = _gpuSampleCount > 0 ? _gpuPeak : null,
            PeakGpuTemperatureC = _gpuTemperaturePeak, SafetyStop = _safetyStop, Analysis = analysis
        };
        _history.Add(result);
        HistoryList.ItemsSource = _history.GetRecent();
        AnalysisText.Text = analysis;
        AppLog.Write($"TEST DE CHARGE TERMINÉ | {result.HeaderText} | {result.MetricsText} | {analysis}");
    }

    private string BuildAnalysis()
    {
        if (_safetyStop) return "Température critique : test coupé automatiquement. Vérifiez le refroidissement avant un nouveau test.";
        var parts = new List<string>();
        if (_cpuTestEnabled)
        {
            var average = _sampleCount > 0 ? _cpuTotal / _sampleCount : 0;
            parts.Add(average >= _targetLoad * .7 ? "Charge CPU atteinte correctement" : "charge CPU inférieure au niveau demandé");
        }
        if (_gpuTestEnabled)
        {
            if (_gpuSampleCount == 0) parts.Add("mesures GPU indisponibles");
            else parts.Add(_gpuPeak >= 70 ? "GPU fortement sollicité" : _gpuPeak >= 35 ? "GPU modérément sollicité" : "rendu GPU peu exigeant sur cette configuration");
            if (_gpuTemperaturePeak.HasValue)
                parts.Add(_gpuTemperaturePeak < 75 ? "température GPU confortable" : _gpuTemperaturePeak < 85 ? "température GPU élevée mais acceptable" : "température GPU proche de la limite");
        }
        return string.Join(" ; ", parts) + ".";
    }

    private void ResetMetrics()
    {
        _sampleCount = _gpuSampleCount = 0;
        _cpuTotal = _cpuPeak = _gpuTotal = _gpuPeak = 0;
        _gpuTemperaturePeak = null;
        _safetyStop = false;
        AnalysisText.Text = "Analyse en cours…";
    }

    private void SetControls(bool idle)
    {
        StartButton.IsEnabled = idle;
        StopButton.IsEnabled = !idle;
        TestTypeBox.IsEnabled = LevelBox.IsEnabled = DurationBox.IsEnabled = idle;
    }
}
