using System.Diagnostics;

namespace PersonalAppsHub.Services;

public sealed class RuntimeDiagnosticsService : IDisposable
{
    private const long WarningThreshold = 500L * 1024 * 1024;
    private const long GrowthThreshold = 150L * 1024 * 1024;
    private readonly System.Threading.Timer _timer;
    private long _lastWorkingSet;

    public RuntimeDiagnosticsService()
    {
        using var process = Process.GetCurrentProcess();
        _lastWorkingSet = process.WorkingSet64;
        AppLog.Write($"DÉMARRAGE | OS={Environment.OSVersion}; .NET={Environment.Version}; " +
                     $"64bits={Environment.Is64BitProcess}; Processeurs={Environment.ProcessorCount}; {AppLog.MemorySnapshot()}");
        _timer = new System.Threading.Timer(SampleMemory, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    private void SampleMemory(object? state)
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var current = process.WorkingSet64;
            var growth = current - Interlocked.Exchange(ref _lastWorkingSet, current);
            if (current >= WarningThreshold || growth >= GrowthThreshold)
                AppLog.Write($"ALERTE MÉMOIRE | CroissanceMinute={growth / (1024 * 1024)} MiB; {AppLog.MemorySnapshot()}");
        }
        catch (Exception ex) { AppLog.WriteException("ERREUR DU MONITEUR MÉMOIRE", ex); }
    }

    public void Dispose()
    {
        _timer.Dispose();
        AppLog.Write($"ARRÊT | {AppLog.MemorySnapshot()}");
    }
}
