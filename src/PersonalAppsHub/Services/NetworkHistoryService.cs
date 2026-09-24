using System.IO;
using System.Text.Json;

namespace PersonalAppsHub.Services;

public sealed class NetworkHistoryService
{
    private const int MaximumSamples = 60;
    private readonly string _path;
    private readonly List<NetworkHistorySample> _samples;

    public NetworkHistoryService(string? path = null)
    {
        _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PersonalAppsHub", "network-history.json");
        _samples = Load();
    }

    public IReadOnlyList<NetworkHistorySample> GetRecent() => _samples.ToArray();

    public void Add(NetworkHistorySample sample)
    {
        _samples.Add(sample);
        if (_samples.Count > MaximumSamples)
            _samples.RemoveRange(0, _samples.Count - MaximumSamples);
        Save();
    }

    public void Clear()
    {
        _samples.Clear();
        Save();
    }

    private List<NetworkHistorySample> Load()
    {
        try
        {
            if (!File.Exists(_path)) return [];
            return (JsonSerializer.Deserialize<List<NetworkHistorySample>>(File.ReadAllText(_path)) ?? [])
                .Where(sample => sample.RecordedAt > DateTime.Now.AddDays(-7))
                .TakeLast(MaximumSamples)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            AppLog.Write($"Historique réseau illisible : {ex.Message}");
            return [];
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(_samples));
            File.Move(temporary, _path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"Historique réseau indisponible : {ex.Message}");
        }
    }
}

public sealed record NetworkHistorySample(
    DateTime RecordedAt, string Source, double PingMs, double JitterMs, double PacketLossPercent);
