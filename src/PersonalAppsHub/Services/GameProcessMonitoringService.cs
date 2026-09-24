using System.Diagnostics;

namespace PersonalAppsHub.Services;

public sealed record GameProcessMetrics(int ProcessId, double? CpuPercent, double? RamMb,
    double? GpuPercent, double? VramMb);

public sealed class GameProcessMonitoringService
{
    private readonly Dictionary<int, (TimeSpan CpuTime, DateTime CapturedAt)> _previousCpu = new();
    private readonly Dictionary<string, PerformanceCounter> _gpuCounters = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PerformanceCounter> _vramCounters = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<int, GameProcessMetrics> Capture(IReadOnlyList<ActiveGameSession> sessions)
    {
        var activeIds = sessions.Select(session => session.ProcessId).ToHashSet();
        var gpu = ReadGpuCounters(activeIds, "GPU Engine", "Utilization Percentage", _gpuCounters, true);
        var vram = ReadGpuCounters(activeIds, "GPU Process Memory", "Dedicated Usage", _vramCounters, false);
        var now = DateTime.UtcNow;
        var result = new Dictionary<int, GameProcessMetrics>();

        foreach (var session in sessions)
        {
            double? cpuPercent = null;
            double? ramMb = null;
            try
            {
                using var process = Process.GetProcessById(session.ProcessId);
                var cpuTime = process.TotalProcessorTime;
                ramMb = process.WorkingSet64 / 1024d / 1024d;
                if (_previousCpu.TryGetValue(session.ProcessId, out var previous))
                {
                    var elapsed = (now - previous.CapturedAt).TotalSeconds;
                    if (elapsed > 0)
                        cpuPercent = Math.Clamp((cpuTime - previous.CpuTime).TotalSeconds /
                            (elapsed * Environment.ProcessorCount) * 100d, 0, 100);
                }
                _previousCpu[session.ProcessId] = (cpuTime, now);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception) { }

            result[session.ProcessId] = new GameProcessMetrics(session.ProcessId, cpuPercent, ramMb,
                gpu.GetValueOrDefault(session.ProcessId), vram.GetValueOrDefault(session.ProcessId));
        }

        foreach (var processId in _previousCpu.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            _previousCpu.Remove(processId);
        CleanupCounters(_gpuCounters, activeIds);
        CleanupCounters(_vramCounters, activeIds);
        return result;
    }

    private static Dictionary<int, double?> ReadGpuCounters(HashSet<int> processIds, string categoryName,
        string counterName, Dictionary<string, PerformanceCounter> counters, bool clampPercent)
    {
        var totals = processIds.ToDictionary(id => id, _ => (double?)null);
        try
        {
            var category = new PerformanceCounterCategory(categoryName);
            foreach (var instance in category.GetInstanceNames())
            {
                var processId = ParseProcessId(instance);
                if (!processId.HasValue || !processIds.Contains(processId.Value)) continue;
                var key = $"{categoryName}|{instance}";
                if (!counters.TryGetValue(key, out var counter))
                {
                    counter = new PerformanceCounter(categoryName, counterName, instance, true);
                    counters[key] = counter;
                    _ = counter.NextValue();
                    continue;
                }
                var value = counter.NextValue();
                if (!float.IsFinite(value) || value < 0) continue;
                if (!clampPercent) value /= 1024f * 1024f;
                totals[processId.Value] = (totals[processId.Value] ?? 0) + value;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { }

        if (clampPercent)
            foreach (var processId in totals.Keys.ToArray())
                if (totals[processId].HasValue) totals[processId] = Math.Clamp(totals[processId]!.Value, 0, 100);
        return totals;
    }

    private static int? ParseProcessId(string instance)
    {
        var marker = instance.IndexOf("pid_", StringComparison.OrdinalIgnoreCase);
        if (marker < 0) return null;
        var start = marker + 4;
        var end = instance.IndexOf('_', start);
        var value = end < 0 ? instance[start..] : instance[start..end];
        return int.TryParse(value, out var processId) ? processId : null;
    }

    private static void CleanupCounters(Dictionary<string, PerformanceCounter> counters, HashSet<int> activeIds)
    {
        foreach (var entry in counters.Where(entry =>
        {
            var processId = ParseProcessId(entry.Key);
            return processId.HasValue && !activeIds.Contains(processId.Value);
        }).ToArray())
        {
            entry.Value.Dispose();
            counters.Remove(entry.Key);
        }
    }
}
