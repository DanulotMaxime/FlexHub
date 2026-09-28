using Microsoft.Win32;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PersonalAppsHub.Services;

public sealed record InstalledApplicationEntry(string Name, string Publisher, string Version,
    DateTime? InstallDate, DateTime? LastUsed, string ExecutableName, string Category = "Logiciel",
    string Source = "Windows", long? SizeBytes = null)
{
    public string InstallDateText => InstallDate?.ToString("dd/MM/yyyy") ?? "Inconnue";
    public string LastUsedText => LastUsed?.ToString("dd/MM/yyyy HH:mm") ?? "Inconnue";
    public string SizeText => SizeBytes.HasValue && SizeBytes.Value > 0
        ? TemporaryFileCleanupService.FormatSize(SizeBytes.Value) : "Inconnue";
    public string UsageStatus(int thresholdDays) => LastUsed.HasValue
        ? LastUsed.Value <= DateTime.Now.AddDays(-thresholdDays) ? "Potentiellement inutilisé" : "Utilisé récemment"
        : "Usage inconnu";
}

public sealed record InstalledApplicationAuditResult(IReadOnlyList<InstalledApplicationEntry> Applications,
    int InaccessibleSources);

public sealed class InstalledApplicationAuditService
{
    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string UserAssistPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist";

    public Task<InstalledApplicationAuditResult> ScanAsync() => Task.Run(Scan);

    private static InstalledApplicationAuditResult Scan()
    {
        var lastRuns = ReadUserAssist();
        var applications = new List<InstalledApplicationEntry>();
        var inaccessible = 0;
        ReadInstalled(applications, RegistryHive.CurrentUser, RegistryView.Default, lastRuns, ref inaccessible);
        ReadInstalled(applications, RegistryHive.LocalMachine, RegistryView.Registry64, lastRuns, ref inaccessible);
        ReadInstalled(applications, RegistryHive.LocalMachine, RegistryView.Registry32, lastRuns, ref inaccessible);
        ReadSteamGames(applications, ref inaccessible);
        ReadEpicGames(applications, lastRuns, ref inaccessible);
        var unique = applications
            .GroupBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(app => app.Category == "Jeu")
                .ThenByDescending(app => app.LastUsed).First())
            .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        return new InstalledApplicationAuditResult(unique, inaccessible);
    }

    private static void ReadSteamGames(List<InstalledApplicationEntry> applications, ref int inaccessible)
    {
        try
        {
            var steamPath = ReadSteamInstallPath();
            if (steamPath is null) return;
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steamPath };
            var libraryFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(libraryFile))
                foreach (Match match in Regex.Matches(File.ReadAllText(libraryFile), "\\\"path\\\"\\s+\\\"(?<path>[^\\\"]+)\\\"", RegexOptions.IgnoreCase))
                    libraries.Add(match.Groups["path"].Value.Replace("\\\\", "\\"));

            foreach (var library in libraries)
            {
                var steamApps = Path.Combine(library, "steamapps");
                if (!Directory.Exists(steamApps)) continue;
                foreach (var manifest in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf"))
                try
                {
                    var text = File.ReadAllText(manifest);
                    var name = VdfValue(text, "name");
                    if (string.IsNullOrWhiteSpace(name) || name.Equals("Steamworks Common Redistributables", StringComparison.OrdinalIgnoreCase)) continue;
                    var lastPlayedText = VdfValue(text, "LastPlayed");
                    DateTime? lastPlayed = long.TryParse(lastPlayedText, out var unix) && unix > 0
                        ? DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime : null;
                    var sizeText = VdfValue(text, "SizeOnDisk");
                    long? sizeBytes = long.TryParse(sizeText, out var parsedSize) && parsedSize > 0 ? parsedSize : null;
                    applications.Add(new InstalledApplicationEntry(name, "Steam", "—",
                        File.GetCreationTime(manifest), lastPlayed, "steam.exe", "Jeu", "Steam", sizeBytes));
                }
                catch { inaccessible++; }
            }
        }
        catch { inaccessible++; }
    }

    private static void ReadEpicGames(List<InstalledApplicationEntry> applications,
        IReadOnlyDictionary<string, DateTime> lastRuns, ref int inaccessible)
    {
        var manifests = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(manifests)) return;
        foreach (var manifest in Directory.EnumerateFiles(manifests, "*.item"))
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifest));
            var root = document.RootElement;
            var name = JsonText(root, "DisplayName") ?? JsonText(root, "AppName");
            if (string.IsNullOrWhiteSpace(name)) continue;
            var launchExecutable = JsonText(root, "LaunchExecutable");
            var executable = string.IsNullOrWhiteSpace(launchExecutable) ? "Inconnu" : Path.GetFileName(launchExecutable);
            DateTime? lastUsed = lastRuns.TryGetValue(executable, out var run) ? run : null;
            long? sizeBytes = JsonInteger(root, "InstallSize");
            applications.Add(new InstalledApplicationEntry(name, "Epic Games", "—",
                File.GetCreationTime(manifest), lastUsed, executable, "Jeu", "Epic Games", sizeBytes));
        }
        catch { inaccessible++; }
    }

    private static string? ReadSteamInstallPath()
    {
        foreach (var path in new[] { @"Software\Valve\Steam", @"SOFTWARE\WOW6432Node\Valve\Steam" })
        {
            using var key = Registry.CurrentUser.OpenSubKey(path) ?? Registry.LocalMachine.OpenSubKey(path);
            var value = key?.GetValue("SteamPath")?.ToString() ?? key?.GetValue("InstallPath")?.ToString();
            if (!string.IsNullOrWhiteSpace(value) && Directory.Exists(value)) return value;
        }
        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        return Directory.Exists(fallback) ? fallback : null;
    }

    private static string? VdfValue(string text, string key)
    {
        var match = Regex.Match(text, $"\\\"{Regex.Escape(key)}\\\"\\s+\\\"(?<value>[^\\\"]*)\\\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string? JsonText(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long? JsonInteger(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.TryGetInt64(out var number) && number > 0 ? number : null;

    private static void ReadInstalled(List<InstalledApplicationEntry> applications, RegistryHive hive,
        RegistryView view, IReadOnlyDictionary<string, DateTime> lastRuns, ref int inaccessible)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = baseKey.OpenSubKey(UninstallPath);
            if (uninstall is null) return;
            foreach (var keyName in uninstall.GetSubKeyNames())
            try
            {
                using var key = uninstall.OpenSubKey(keyName);
                var name = key?.GetValue("DisplayName")?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(name) || Convert.ToInt32(key?.GetValue("SystemComponent") ?? 0) == 1) continue;
                var publisher = key?.GetValue("Publisher")?.ToString()?.Trim() ?? "Éditeur inconnu";
                var version = key?.GetValue("DisplayVersion")?.ToString()?.Trim() ?? "—";
                var installDate = ParseInstallDate(key?.GetValue("InstallDate")?.ToString());
                var executable = ExtractExecutableName(key?.GetValue("DisplayIcon")?.ToString());
                var estimatedSizeKb = Convert.ToInt64(key?.GetValue("EstimatedSize") ?? 0);
                long? sizeBytes = estimatedSizeKb > 0 && estimatedSizeKb <= long.MaxValue / 1024
                    ? estimatedSizeKb * 1024 : null;
                DateTime? lastUsed = executable is not null && lastRuns.TryGetValue(executable, out var run) ? run : null;
                applications.Add(new InstalledApplicationEntry(name, publisher, version, installDate, lastUsed,
                    executable ?? "Inconnu", SizeBytes: sizeBytes));
            }
            catch { inaccessible++; }
        }
        catch { inaccessible++; }
    }

    private static Dictionary<string, DateTime> ReadUserAssist()
    {
        var result = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(UserAssistPath);
            if (root is null) return result;
            foreach (var guid in root.GetSubKeyNames())
            {
                using var count = root.OpenSubKey($@"{guid}\Count");
                if (count is null) continue;
                foreach (var encodedName in count.GetValueNames())
                {
                    var data = count.GetValue(encodedName) as byte[];
                    if (data is null || data.Length < 68) continue;
                    var fileTime = BitConverter.ToInt64(data, 60);
                    if (fileTime <= 0) continue;
                    DateTime lastRun;
                    try { lastRun = DateTime.FromFileTimeUtc(fileTime).ToLocalTime(); }
                    catch { continue; }
                    var decoded = Rot13(encodedName);
                    var executable = Path.GetFileName(decoded.Split('?', 2)[0]);
                    if (string.IsNullOrWhiteSpace(executable) || !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!result.TryGetValue(executable, out var previous) || lastRun > previous) result[executable] = lastRun;
                }
            }
        }
        catch { }
        return result;
    }

    private static string? ExtractExecutableName(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon)) return null;
        var value = displayIcon.Trim().Trim('"');
        var comma = value.LastIndexOf(',');
        if (comma > 0 && int.TryParse(value[(comma + 1)..], out _)) value = value[..comma].Trim('"');
        var name = Path.GetFileName(value);
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name : null;
    }

    private static DateTime? ParseInstallDate(string? value) =>
        DateTime.TryParseExact(value, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date) ? date : null;

    private static string Rot13(string value) => new(value.Select(character => character switch
    {
        >= 'a' and <= 'm' or >= 'A' and <= 'M' => (char)(character + 13),
        >= 'n' and <= 'z' or >= 'N' and <= 'Z' => (char)(character - 13),
        _ => character
    }).ToArray());
}
