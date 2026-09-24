using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonalAppsHub.Services;

public sealed class StressTestHistoryService
{
    private const int MaximumResults = 30;
    private readonly string _path;
    private readonly List<StressTestResult> _results;

    public StressTestHistoryService(string? path = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PersonalAppsHub", "stress-tests.json");
        _results = Load();
    }

    public IReadOnlyList<StressTestResult> GetRecent(int count = 5) =>
        _results.OrderByDescending(result => result.StartedAt).Take(count).ToArray();

    public void Add(StressTestResult result)
    {
        _results.Add(result);
        if (_results.Count > MaximumResults)
            _results.RemoveRange(0, _results.Count - MaximumResults);
        Save();
    }

    private List<StressTestResult> Load()
    {
        try
        {
            if (!File.Exists(_path)) return [];
            return JsonSerializer.Deserialize<List<StressTestResult>>(File.ReadAllText(_path)) ?? [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            AppLog.Write($"Historique des tests de charge illisible : {ex.Message}");
            return [];
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(_results));
            File.Move(temporary, _path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Historique des tests de charge indisponible : {ex.Message}");
        }
    }
}

public sealed class StressTestResult
{
    public DateTime StartedAt { get; set; }
    public int DurationSeconds { get; set; }
    public string Level { get; set; } = "";
    public bool CpuEnabled { get; set; }
    public bool GpuEnabled { get; set; }
    public double AverageCpuPercent { get; set; }
    public double PeakCpuPercent { get; set; }
    public double? AverageGpuPercent { get; set; }
    public double? PeakGpuPercent { get; set; }
    public double? PeakGpuTemperatureC { get; set; }
    public bool SafetyStop { get; set; }
    public string Analysis { get; set; } = "";
    [JsonIgnore] public string TestTypeText => CpuEnabled && GpuEnabled ? "CPU + GPU" : CpuEnabled ? "CPU" : "GPU";
    [JsonIgnore] public string HeaderText => $"{StartedAt:dd/MM/yyyy HH:mm} · {TestTypeText} · {Level} · {DurationSeconds} s";
    [JsonIgnore] public string MetricsText => $"CPU moy./max {AverageCpuPercent:0}/{PeakCpuPercent:0}% · GPU moy./max {(AverageGpuPercent.HasValue ? $"{AverageGpuPercent:0}/{PeakGpuPercent:0}%" : "—")} · GPU max {(PeakGpuTemperatureC.HasValue ? $"{PeakGpuTemperatureC:0} °C" : "—")}";
}
