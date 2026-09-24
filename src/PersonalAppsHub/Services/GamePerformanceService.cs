using System.Diagnostics;

namespace PersonalAppsHub.Services;

public sealed class GamePerformanceService : IDisposable
{
    private readonly Dictionary<int, TrackedPriority> _tracked = new();
    private readonly Dictionary<int, DateTime> _failed = new();

    public GamePerformanceResult Update(IReadOnlyList<ActiveGameSession> sessions, bool enabled)
    {
        var activeIds = sessions.Select(session => session.ProcessId).ToHashSet();
        foreach (var endedId in _tracked.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            _tracked.Remove(endedId);
        foreach (var endedId in _failed.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            _failed.Remove(endedId);
        if (!enabled)
        {
            var restored = RestoreAll();
            return new GamePerformanceResult(0, restored, 0, restored > 0
                ? $"Priorité d’origine restaurée pour {restored} jeu(x)."
                : "Mode performance désactivé.");
        }

        var applied = 0;
        var failures = 0;
        foreach (var session in sessions)
        {
            if (_tracked.ContainsKey(session.ProcessId) ||
                _failed.TryGetValue(session.ProcessId, out var failedStart) && failedStart == session.StartedAt) continue;
            try
            {
                using var process = Process.GetProcessById(session.ProcessId);
                if (process.StartTime != session.StartedAt) continue;
                var original = process.PriorityClass;
                process.PriorityClass = ProcessPriorityClass.High;
                _tracked[session.ProcessId] = new TrackedPriority(session.StartedAt, original, session.Name);
                applied++;
                AppLog.Write($"MODE PERFORMANCE | Priorité Haute appliquée à {session.Name} (PID {session.ProcessId}), origine={original}");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                failures++;
                _failed[session.ProcessId] = session.StartedAt;
                AppLog.Write($"MODE PERFORMANCE | Impossible de modifier {session.Name} (PID {session.ProcessId}) : {ex.Message}");
            }
        }
        var active = _tracked.Count;
        var message = active > 0
            ? $"Priorité Haute active pour {active} jeu(x)."
            : sessions.Count == 0 ? "Mode prêt : aucun jeu actif détecté."
            : "Le jeu a été détecté, mais sa priorité n’a pas pu être modifiée.";
        return new GamePerformanceResult(applied, 0, failures, message);
    }

    public int RestoreAll()
    {
        var restored = 0;
        foreach (var pair in _tracked.ToArray())
        {
            try
            {
                using var process = Process.GetProcessById(pair.Key);
                if (process.StartTime == pair.Value.StartedAt)
                {
                    process.PriorityClass = pair.Value.OriginalPriority;
                    restored++;
                    AppLog.Write($"MODE PERFORMANCE | Priorité {pair.Value.OriginalPriority} restaurée pour {pair.Value.Name} (PID {pair.Key})");
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                AppLog.Write($"MODE PERFORMANCE | Restauration impossible pour PID {pair.Key} : {ex.Message}");
            }
        }
        _tracked.Clear();
        _failed.Clear();
        return restored;
    }

    public void Dispose() => RestoreAll();
    private sealed record TrackedPriority(DateTime StartedAt, ProcessPriorityClass OriginalPriority, string Name);
}

public sealed record GamePerformanceResult(int AppliedCount, int RestoredCount, int FailureCount, string Message);
