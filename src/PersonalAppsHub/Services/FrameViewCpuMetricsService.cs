using System.IO;
using System.Runtime.InteropServices;

namespace PersonalAppsHub.Services;

public readonly record struct FrameViewCpuMetrics(double? PackagePowerWatts, double? PackageTemperatureC);

public sealed class FrameViewCpuMetricsService : IDisposable
{
    private const int Success = 0;
    private readonly object _sync = new();
    private nint _library;
    private StartDelegate? _start;
    private CaptureDelegate? _capture;
    private StopDelegate? _stop;
    private bool _started;
    private bool _unavailable;
    private DateTime _retryAfterUtc;
    private bool _loggedActive;

    public static bool IsInstalled => OperatingSystem.IsWindows() &&
        File.Exists(Path.Combine(AppContext.BaseDirectory, "Tools", "FlexHub.FrameViewBridge.dll")) &&
        Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "NVIDIA Corporation", "FrameViewSDK"));

    public FrameViewCpuMetrics Capture()
    {
        lock (_sync)
        {
            if (!EnsureStarted()) return default;
            var status = _capture!(out var power, out var temperature);
            if (status != Success) return default;
            if (!_loggedActive)
            {
                AppLog.Write("Télémétrie CPU NVIDIA FrameView active.");
                _loggedActive = true;
            }
            return new FrameViewCpuMetrics(
                power is > 0.5 and < 1000 ? power : null,
                temperature is > 0 and < 125 ? temperature : null);
        }
    }

    private bool EnsureStarted()
    {
        if (_started) return true;
        if (!OperatingSystem.IsWindows()) return false;
        if (_unavailable && DateTime.UtcNow < _retryAfterUtc) return false;
        _unavailable = false;
        try
        {
            var bridgePath = Path.Combine(AppContext.BaseDirectory, "Tools", "FlexHub.FrameViewBridge.dll");
            if (!File.Exists(bridgePath)) { _unavailable = true; return false; }
            _library = NativeLibrary.Load(bridgePath);
            _start = Marshal.GetDelegateForFunctionPointer<StartDelegate>(NativeLibrary.GetExport(_library, "FlexHubFrameView_Start"));
            _capture = Marshal.GetDelegateForFunctionPointer<CaptureDelegate>(NativeLibrary.GetExport(_library, "FlexHubFrameView_Capture"));
            _stop = Marshal.GetDelegateForFunctionPointer<StopDelegate>(NativeLibrary.GetExport(_library, "FlexHubFrameView_Stop"));
            var status = _start();
            if (status != Success)
            {
                AppLog.Write($"NVIDIA FrameView indisponible (code {status}); utilisation des capteurs de secours.");
                ReleaseLibrary();
                _unavailable = true;
                _retryAfterUtc = DateTime.UtcNow.AddMinutes(1);
                return false;
            }
            _started = true;
            Thread.Sleep(1100);
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Write($"NVIDIA FrameView indisponible : {ex.Message}");
            ReleaseLibrary();
            _unavailable = true;
            _retryAfterUtc = DateTime.UtcNow.AddMinutes(1);
            return false;
        }
    }

    public void Dispose()
    {
        lock (_sync) ReleaseLibrary();
    }

    private void ReleaseLibrary()
    {
        if (_started)
        {
            try { _stop?.Invoke(); } catch { }
        }
        _started = false;
        _start = null;
        _capture = null;
        _stop = null;
        if (_library != 0)
        {
            NativeLibrary.Free(_library);
            _library = 0;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int StartDelegate();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CaptureDelegate(out double packagePowerWatts, out double packageTemperatureC);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void StopDelegate();
}
