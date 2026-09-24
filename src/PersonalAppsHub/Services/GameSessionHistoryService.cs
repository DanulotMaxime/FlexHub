using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonalAppsHub.Services;

public sealed class GameSessionReport
{
    public int ProcessId { get; set; }
    public string GameName { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool IsActive { get; set; }
    public double? PeakGpuTemperatureC { get; set; }
    public double? PeakGpuUsagePercent { get; set; }
    public double? PeakCpuUsagePercent { get; set; }
    public double? PeakRamUsagePercent { get; set; }
    public double GpuUsageTotal { get; set; }
    public int GpuUsageSampleCount { get; set; }
    public double? PeakProcessCpuPercent { get; set; }
    public double? PeakProcessRamMb { get; set; }
    public double? PeakProcessGpuPercent { get; set; }
    public double ProcessGpuUsageTotal { get; set; }
    public int ProcessGpuUsageSampleCount { get; set; }
    public double? PeakProcessVramMb { get; set; }
    public bool IsReference { get; set; }
    public string NvidiaProfileName { get; set; } = "Non identifié";
    [JsonIgnore] public bool CanSetReference => !IsActive;
    [JsonIgnore] public string ReferenceButtonText => IsReference ? "Référence active" : "Choisir référence";
    [JsonIgnore] public string ReferenceStarText => IsReference ? "★" : "☆";
    [JsonIgnore] public string ComparisonText { get; set; } = "";
    [JsonIgnore] public string NvidiaProfileText => $"Profil NVIDIA : {NvidiaProfileName}";
    public string DateText => StartedAt.ToString("dd/MM/yyyy");
    public string PeriodText => $"{StartedAt:HH:mm} → {(IsActive ? "en cours" : LastSeenAt.ToString("HH:mm"))}";
    public string DurationText
    {
        get
        {
            var duration = (IsActive ? DateTime.Now : LastSeenAt) - StartedAt;
            return duration.TotalHours >= 1 ? $"{(int)duration.TotalHours} h {duration.Minutes:00} min" : $"{Math.Max(0, (int)duration.TotalMinutes)} min";
        }
    }
    public string PeakGpuTemperatureText => PeakGpuTemperatureC.HasValue ? $"{PeakGpuTemperatureC:0} °C" : "—";
    public string CpuPeakText => PeakCpuUsagePercent.HasValue ? $"{PeakCpuUsagePercent:0}%" : "—";
    public string RamPeakText => PeakRamUsagePercent.HasValue ? $"{PeakRamUsagePercent:0}%" : "—";
    public string GpuAverageText => GpuUsageSampleCount > 0 ? $"{GpuUsageTotal / GpuUsageSampleCount:0}%" : "—";
    public string GpuUsagePeakText => PeakGpuUsagePercent.HasValue ? $"{PeakGpuUsagePercent:0}%" : "—";
    public string ProcessCpuPeakText => PeakProcessCpuPercent.HasValue ? $"{PeakProcessCpuPercent:0.0}%" : "—";
    public string ProcessRamPeakText => PeakProcessRamMb.HasValue ? $"{PeakProcessRamMb:0} Mo" : "—";
    public string ProcessGpuAverageText => ProcessGpuUsageSampleCount > 0 ? $"{ProcessGpuUsageTotal / ProcessGpuUsageSampleCount:0.0}%" : "—";
    public string ProcessGpuPeakText => PeakProcessGpuPercent.HasValue ? $"{PeakProcessGpuPercent:0.0}%" : "—";
    public string ProcessVramPeakText => PeakProcessVramMb.HasValue ? $"{PeakProcessVramMb:0} Mo" : "—";
    public string ProcessPerformanceText => $"JEU · CPU max {ProcessCpuPeakText} · RAM max {ProcessRamPeakText} · GPU moy./max {ProcessGpuAverageText} / {ProcessGpuPeakText} · VRAM max {ProcessVramPeakText}";
    public string GpuPeakText => PeakGpuUsagePercent.HasValue || PeakGpuTemperatureC.HasValue
        ? $"{(PeakGpuUsagePercent.HasValue ? $"{PeakGpuUsagePercent:0}%" : "—")} / {(PeakGpuTemperatureC.HasValue ? $"{PeakGpuTemperatureC:0} °C" : "—")}" : "—";
    public string PerformanceText
    {
        get
        {
            var cpu = PeakCpuUsagePercent.HasValue ? $"CPU max {PeakCpuUsagePercent:0}%" : "CPU —";
            var ram = PeakRamUsagePercent.HasValue ? $"RAM max {PeakRamUsagePercent:0}%" : "RAM —";
            var gpuAverage = GpuUsageSampleCount > 0 ? $"GPU moy. {GpuUsageTotal / GpuUsageSampleCount:0}%" : "GPU moy. —";
            return $"{cpu} · {ram} · {gpuAverage} · GPU max {GpuPeakText}";
        }
    }
}

public sealed record WeeklyGameSummary(string GameName, int SessionCount, TimeSpan TotalDuration, double RelativePercent = 0)
{
    public string SessionCountText => SessionCount == 1 ? "1 session" : $"{SessionCount} sessions";
    public string TotalDurationText => TotalDuration.TotalHours >= 1
        ? $"{(int)TotalDuration.TotalHours} h {TotalDuration.Minutes:00} min"
        : $"{Math.Max(0, (int)TotalDuration.TotalMinutes)} min";
}

public sealed class GameSessionHistoryService
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PersonalAppsHub", "game-sessions.json");
    private readonly List<GameSessionReport> _reports;
    private DateTime _lastSaveUtc = DateTime.MinValue;

    public GameSessionHistoryService() => _reports = Load();

    public IReadOnlyList<GameSessionReport> Update(IReadOnlyList<ActiveGameSession> sessions, string? activeNvidiaProfileName = null)
    {
        var now = DateTime.Now;
        _reports.RemoveAll(report => IsUtilityProcess(report.GameName));
        foreach (var report in _reports.Where(report => report.IsActive)) report.IsActive = false;
        foreach (var session in sessions)
        {
            var report = _reports.FirstOrDefault(item => item.ProcessId == session.ProcessId && item.StartedAt == session.StartedAt);
            if (report is null)
            {
                report = new GameSessionReport
                {
                    ProcessId = session.ProcessId,
                    GameName = session.Name,
                    StartedAt = session.StartedAt,
                    NvidiaProfileName = string.IsNullOrWhiteSpace(activeNvidiaProfileName) ? "Non identifié" : activeNvidiaProfileName
                };
                _reports.Add(report);
            }
            report.GameName = session.Name;
            report.LastSeenAt = now;
            report.IsActive = true;
        }
        Save();
        UpdateComparisons();
        return _reports.OrderByDescending(report => report.StartedAt).Take(10).ToArray();
    }

    public IReadOnlyList<WeeklyGameSummary> GetSummary(DateTime? start)
    {
        var now = DateTime.Now;
        var summaries = _reports.Where(report => !start.HasValue || report.LastSeenAt >= start.Value)
            .GroupBy(report => report.GameName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new WeeklyGameSummary(group.First().GameName, group.Count(),
                TimeSpan.FromTicks(group.Sum(report =>
                    Math.Max(0, ((report.IsActive ? now : report.LastSeenAt) - report.StartedAt).Ticks)))))
            .OrderByDescending(summary => summary.TotalDuration)
            .ToArray();
        var longest = summaries.Select(summary => summary.TotalDuration.TotalSeconds).DefaultIfEmpty(0).Max();
        return summaries.Select(summary => summary with
        {
            RelativePercent = longest <= 0 ? 0 : summary.TotalDuration.TotalSeconds / longest * 100
        }).ToArray();
    }

    public IReadOnlyList<GameSessionReport> GetAllReports() =>
        _reports.OrderByDescending(report => report.StartedAt).ToArray();

    public bool SetReference(GameSessionReport selected)
    {
        var report = _reports.FirstOrDefault(item => item.ProcessId == selected.ProcessId && item.StartedAt == selected.StartedAt);
        if (report is null || report.IsActive) return false;
        foreach (var item in _reports.Where(item => item.GameName.Equals(report.GameName, StringComparison.OrdinalIgnoreCase)))
            item.IsReference = false;
        report.IsReference = true;
        UpdateComparisons();
        Save(true);
        return true;
    }

    public IReadOnlyList<GameSessionReport> GetRecentReports()
    {
        UpdateComparisons();
        return _reports.OrderByDescending(report => report.StartedAt).Take(10).ToArray();
    }

    public void Clear()
    {
        _reports.Clear();
        Save(true);
    }

    public void RecordPerformanceMetrics(double cpuPercent, double ramPercent, double? gpuUsagePercent, double? gpuTemperatureC)
    {
        var changed = false;
        foreach (var report in _reports.Where(report => report.IsActive))
        {
            if (!report.PeakCpuUsagePercent.HasValue || cpuPercent > report.PeakCpuUsagePercent.Value)
            {
                report.PeakCpuUsagePercent = cpuPercent;
                changed = true;
            }
            if (!report.PeakRamUsagePercent.HasValue || ramPercent > report.PeakRamUsagePercent.Value)
            {
                report.PeakRamUsagePercent = ramPercent;
                changed = true;
            }
            if (gpuTemperatureC.HasValue && (!report.PeakGpuTemperatureC.HasValue || gpuTemperatureC.Value > report.PeakGpuTemperatureC.Value))
            {
                report.PeakGpuTemperatureC = gpuTemperatureC.Value;
                changed = true;
            }
            if (gpuUsagePercent.HasValue)
            {
                report.GpuUsageTotal += gpuUsagePercent.Value;
                report.GpuUsageSampleCount++;
                if (!report.PeakGpuUsagePercent.HasValue || gpuUsagePercent.Value > report.PeakGpuUsagePercent.Value)
                    report.PeakGpuUsagePercent = gpuUsagePercent.Value;
                changed = true;
            }
        }
        if (changed) Save();
    }

    public void RecordProcessMetrics(IEnumerable<GameProcessMetrics> metrics)
    {
        var byProcess = metrics.ToDictionary(metric => metric.ProcessId);
        var changed = false;
        foreach (var report in _reports.Where(report => report.IsActive && byProcess.ContainsKey(report.ProcessId)))
        {
            var sample = byProcess[report.ProcessId];
            if (sample.CpuPercent.HasValue && (!report.PeakProcessCpuPercent.HasValue || sample.CpuPercent > report.PeakProcessCpuPercent))
                report.PeakProcessCpuPercent = sample.CpuPercent;
            if (sample.RamMb.HasValue && (!report.PeakProcessRamMb.HasValue || sample.RamMb > report.PeakProcessRamMb))
                report.PeakProcessRamMb = sample.RamMb;
            if (sample.GpuPercent.HasValue)
            {
                report.ProcessGpuUsageTotal += sample.GpuPercent.Value;
                report.ProcessGpuUsageSampleCount++;
                if (!report.PeakProcessGpuPercent.HasValue || sample.GpuPercent > report.PeakProcessGpuPercent)
                    report.PeakProcessGpuPercent = sample.GpuPercent;
            }
            if (sample.VramMb.HasValue && (!report.PeakProcessVramMb.HasValue || sample.VramMb > report.PeakProcessVramMb))
                report.PeakProcessVramMb = sample.VramMb;
            changed |= sample.CpuPercent.HasValue || sample.RamMb.HasValue || sample.GpuPercent.HasValue || sample.VramMb.HasValue;
        }
        if (changed) Save();
    }

    private static bool IsUtilityProcess(string name) =>
        name.Contains("launcher", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("crashreport", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("crashpad", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("bootstrap", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("config", StringComparison.OrdinalIgnoreCase);

    private void UpdateComparisons()
    {
        foreach (var report in _reports)
        {
            var processDetails = report.ProcessPerformanceText;
            if (report.IsReference)
            {
                report.ComparisonText = processDetails + Environment.NewLine + "★ Session de référence";
                continue;
            }
            var reference = _reports.FirstOrDefault(item => item.IsReference &&
                item.GameName.Equals(report.GameName, StringComparison.OrdinalIgnoreCase));
            if (reference is null || report.StartedAt <= reference.StartedAt)
            {
                report.ComparisonText = processDetails;
                continue;
            }
            var differences = new List<string>();
            var currentDuration = (report.IsActive ? DateTime.Now : report.LastSeenAt) - report.StartedAt;
            var referenceDuration = reference.LastSeenAt - reference.StartedAt;
            var durationDifference = currentDuration - referenceDuration;
            differences.Add($"Durée {durationDifference.TotalMinutes:+0;-0;0} min");
            AddDifference(differences, "CPU", report.PeakCpuUsagePercent, reference.PeakCpuUsagePercent, "%");
            AddDifference(differences, "RAM", report.PeakRamUsagePercent, reference.PeakRamUsagePercent, "%");
            var currentGpuAverage = report.GpuUsageSampleCount > 0 ? report.GpuUsageTotal / report.GpuUsageSampleCount : (double?)null;
            var referenceGpuAverage = reference.GpuUsageSampleCount > 0 ? reference.GpuUsageTotal / reference.GpuUsageSampleCount : (double?)null;
            AddDifference(differences, "GPU moy.", currentGpuAverage, referenceGpuAverage, "%");
            AddDifference(differences, "Temp. GPU", report.PeakGpuTemperatureC, reference.PeakGpuTemperatureC, " °C");
            var comparison = differences.Count == 0 ? "Comparaison indisponible" : "Vs réf. : " + string.Join(" · ", differences);
            report.ComparisonText = processDetails + Environment.NewLine + comparison;
        }
    }

    private static void AddDifference(List<string> target, string label, double? current, double? reference, string suffix)
    {
        if (!current.HasValue || !reference.HasValue) return;
        var difference = current.Value - reference.Value;
        target.Add($"{label} {difference:+0.0;-0.0;0}{suffix}");
    }

    private List<GameSessionReport> Load()
    {
        try
        {
            if (!File.Exists(_path)) return [];
            var reports = JsonSerializer.Deserialize<List<GameSessionReport>>(File.ReadAllText(_path)) ?? [];
            foreach (var report in reports) report.IsActive = false;
            return reports;
        }
        catch { return []; }
    }

    private void Save(bool force = false)
    {
        if (!force && DateTime.UtcNow - _lastSaveUtc < TimeSpan.FromSeconds(30)) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(_reports));
            File.Move(temporary, _path, true);
            _lastSaveUtc = DateTime.UtcNow;
        }
        catch (IOException ex) { AppLog.Write($"Historique des sessions indisponible : {ex.Message}"); }
        catch (UnauthorizedAccessException ex) { AppLog.Write($"Historique des sessions inaccessible : {ex.Message}"); }
    }
}
