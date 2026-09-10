using System.Text.Json;
using System.IO;
using PersonalAppsHub.Models;

namespace PersonalAppsHub.Services;

public sealed class SettingsService
{
    private readonly string _folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PersonalAppsHub");
    private string SettingsPath => Path.Combine(_folder, "settings.json");
    public bool SettingsExist => File.Exists(SettingsPath);
    public HubSettings Load()
    {
        if (!File.Exists(SettingsPath)) return new();
        try
        {
            var settings = JsonSerializer.Deserialize<HubSettings>(File.ReadAllText(SettingsPath)) ?? new();
            settings.SettingsSchemaVersion = 2;
            return settings;
        }
        catch
        {
            try
            {
                var backup = Path.Combine(_folder, $"settings-corrompus-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                File.Copy(SettingsPath, backup, overwrite: false);
            }
            catch { }
            return new();
        }
    }
    public void Save(HubSettings settings)
    {
        Directory.CreateDirectory(_folder);
        settings.SettingsSchemaVersion = 2;
        var temporaryPath = SettingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporaryPath, SettingsPath, overwrite: true);
    }

    public void DeleteAllPersonalData()
    {
        var appDataRoot = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(_folder);
        if (!target.StartsWith(appDataRoot, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetFileName(target), "PersonalAppsHub", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Le dossier de données personnelles n’a pas pu être vérifié.");

        if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
    }
}
