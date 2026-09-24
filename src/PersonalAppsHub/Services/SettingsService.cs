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
            if (settings.SettingsSchemaVersion < 4) settings.MonitoringEnabled = true;
            if (settings.SettingsSchemaVersion < 5)
            {
                settings.MonitoringAlertsEnabled = true;
                settings.MonitoringCpuAlertPercent = 95;
                settings.MonitoringRamAlertPercent = 90;
                settings.MonitoringGpuTemperatureAlertC = 85;
            }
            if (settings.SettingsSchemaVersion < 6) settings.MonitoringCpuTemperatureAlertC = 90;
            if (settings.SettingsSchemaVersion < 7 && string.IsNullOrWhiteSpace(settings.NetworkMonitoringTargets))
                settings.NetworkMonitoringTargets = "passerelle;Discord=discord.com;Steam=store.steampowered.com;jeu";
            if (settings.SettingsSchemaVersion < 8 && !settings.NetworkMonitoringTargets.Split(';', StringSplitOptions.TrimEntries)
                    .Any(target => target.Equals("jeu", StringComparison.OrdinalIgnoreCase)))
                settings.NetworkMonitoringTargets += ";jeu";
            if (settings.SettingsSchemaVersion < 9 && !settings.NetworkMonitoringTargets.Split(';', StringSplitOptions.TrimEntries)
                    .Any(target => target.StartsWith("Internet=", StringComparison.OrdinalIgnoreCase)))
                settings.NetworkMonitoringTargets = settings.NetworkMonitoringTargets.Replace("passerelle", "passerelle;Internet=1.1.1.1", StringComparison.OrdinalIgnoreCase);
            if (settings.SettingsSchemaVersion < 10) settings.GameSessionAlertHours = 2;
            if (settings.SettingsSchemaVersion < 11) settings.GameSessionAlertEnabled = true;
            if (settings.SettingsSchemaVersion < 12)
            {
                settings.NetworkJitterAlertEnabled = true;
                settings.NetworkJitterAlertMs = 30;
            }
            if (settings.SettingsSchemaVersion < 13)
                settings.ActiveNvidiaProfileName = "Non identifié";
            if (settings.SettingsSchemaVersion < 14)
                settings.AutomaticTemporaryCleanupEnabled = false;
            if (settings.SettingsSchemaVersion < 15)
            {
                settings.GameSessionsModuleEnabled = true;
                settings.NetworkMonitoringModuleEnabled = true;
                settings.TemporaryCleanupModuleEnabled = true;
                settings.DuplicateFilesModuleEnabled = true;
                settings.StorageHealthModuleEnabled = true;
                settings.StartupAuditModuleEnabled = true;
            }
            if (settings.SettingsSchemaVersion < 16)
                settings.AutomaticGameHighPriorityEnabled = false;
            if (settings.SettingsSchemaVersion < 17)
                settings.GameSessionAlertEnabled = false;
            settings.SettingsSchemaVersion = 17;
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
        settings.SettingsSchemaVersion = 17;
        var temporaryPath = SettingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporaryPath, SettingsPath, overwrite: true);
        AppLog.Write("CONFIGURATION ENREGISTRÉE");
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
