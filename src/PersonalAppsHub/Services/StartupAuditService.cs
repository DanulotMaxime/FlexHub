using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace PersonalAppsHub.Services;

public sealed record StartupEntry(string Name, string Command, string Source, string Scope, string Status,
    string Recommendation, string Reason, string Publisher, RegistryHive Hive, RegistryView View, string ApprovalPath, string ApprovalName)
{
    public bool IsEnabled => Status == "Actif";
}
public sealed record StartupAuditResult(IReadOnlyList<StartupEntry> Entries, int InaccessibleSources);

public sealed class StartupAuditService
{
    private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedRunPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ApprovedFolderPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    public Task<StartupAuditResult> ScanAsync() => Task.Run(Scan);

    private static StartupAuditResult Scan()
    {
        var entries = new List<StartupEntry>();
        var inaccessible = 0;
        ReadRegistry(entries, RegistryHive.CurrentUser, RegistryView.Default, "Registre", "Utilisateur", ref inaccessible);
        ReadRegistry(entries, RegistryHive.LocalMachine, RegistryView.Registry64, "Registre 64 bits", "Tous les utilisateurs", ref inaccessible);
        ReadRegistry(entries, RegistryHive.LocalMachine, RegistryView.Registry32, "Registre 32 bits", "Tous les utilisateurs", ref inaccessible);
        ReadFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Dossier Démarrage", "Utilisateur", RegistryHive.CurrentUser, ref inaccessible);
        ReadFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Dossier Démarrage", "Tous les utilisateurs", RegistryHive.LocalMachine, ref inaccessible);

        var unique = entries
            .GroupBy(entry => $"{entry.Name}\0{entry.Command}\0{entry.Scope}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        return new StartupAuditResult(unique, inaccessible);
    }

    private static void ReadRegistry(List<StartupEntry> entries, RegistryHive hive, RegistryView view,
        string source, string scope, ref int inaccessible)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var run = baseKey.OpenSubKey(RunPath, writable: false);
            using var approved = baseKey.OpenSubKey(ApprovedRunPath, writable: false);
            if (run is null) return;
            foreach (var name in run.GetValueNames())
            {
                var command = run.GetValue(name)?.ToString();
                if (!string.IsNullOrWhiteSpace(command))
                {
                    var metadata = InspectCommand(command);
                    var (recommendation, reason) = Analyze(name, command, metadata.Publisher, metadata.Description);
                    entries.Add(new StartupEntry(name, command, source, scope, ReadApprovalStatus(approved, name),
                        recommendation, reason, metadata.Publisher, hive, view, ApprovedRunPath, name));
                }
            }
        }
        catch (Exception) { inaccessible++; }
    }

    private static void ReadFolder(List<StartupEntry> entries, string folder, string source, string scope,
        RegistryHive approvalHive, ref int inaccessible)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(approvalHive, RegistryView.Default);
            using var approved = baseKey.OpenSubKey(ApprovedFolderPath, writable: false);
            foreach (var path in Directory.EnumerateFiles(folder))
            {
                var approvalName = Path.GetFileName(path);
                var name = Path.GetFileNameWithoutExtension(path);
                var metadata = InspectCommand(path);
                var (recommendation, reason) = Analyze(name, path, metadata.Publisher, metadata.Description);
                entries.Add(new StartupEntry(name, path, source, scope, ReadApprovalStatus(approved, approvalName),
                    recommendation, reason, metadata.Publisher, approvalHive, RegistryView.Default, ApprovedFolderPath, approvalName));
            }
        }
        catch (Exception) { inaccessible++; }
    }

    public static string ApprovalStatus(byte[]? value) => value is null || value.Length == 0
        ? "Actif"
        : value[0] switch { 2 => "Actif", 3 => "Désactivé", _ => "État inconnu" };

    private static string ReadApprovalStatus(RegistryKey? approved, string name) =>
        ApprovalStatus(approved?.GetValue(name) as byte[]);

    public Task SetEnabledAsync(StartupEntry entry, bool enabled) => Task.Run(() =>
    {
        using var baseKey = RegistryKey.OpenBaseKey(entry.Hive, entry.View);
        using var approved = baseKey.CreateSubKey(entry.ApprovalPath, writable: true)
            ?? throw new InvalidOperationException("Windows n’autorise pas la modification de cette entrée.");
        approved.SetValue(entry.ApprovalName, CreateApprovalValue(enabled), RegistryValueKind.Binary);
    });

    public static byte[] CreateApprovalValue(bool enabled)
    {
        var value = new byte[12];
        value[0] = enabled ? (byte)2 : (byte)3;
        if (!enabled)
            BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(value, 4);
        return value;
    }

    public static (string Recommendation, string Reason) Analyze(string name, string command,
        string publisher = "", string description = "")
    {
        var text = Normalize($"{name} {command} {publisher} {description}");

        if (text.Contains("flexhub"))
            return ("À conserver", "FlexHub est configuré explicitement pour démarrer avec Windows.");

        if (ContainsAny(text, SecurityComponents))
            return ("À conserver",
                "Composant lié à la sécurité, à l’authentification ou à la protection du système.");

        if (ContainsAny(text, DriverComponents))
            return ("À conserver",
                "Pilote ou composant matériel susceptible de gérer le son, la saisie, l’affichage ou un périphérique.");

        if (ContainsAny(text, OptionalApplications))
            return ("Désactivation envisageable",
                "Application de communication, multimédia, création ou lancement de jeux : son démarrage automatique est facultatif et elle restera accessible manuellement.");

        if (ContainsAny(text, UpdateAgents))
            return ("Désactivation envisageable",
                "Assistant de mise à jour ou de lancement rapide non indispensable au démarrage de Windows. Les mises à jour devront parfois être vérifiées depuis l’application.");

        if (ContainsAny(text, SyncAndBackupApplications))
            return ("Selon votre usage",
                "Service de synchronisation ou de sauvegarde. Conservez-le si vous voulez synchroniser vos fichiers dès l’ouverture de session.");

        if (ContainsAny(text, HardwareUtilities))
            return ("Selon votre usage",
                "Utilitaire matériel ou périphérique. Il peut être utile pour les profils, raccourcis, ventilateurs, éclairages ou fonctions avancées.");

        if (ContainsAny(text, GamingServices))
            return ("Selon votre usage",
                "Service de jeu ou anti-triche. Conservez-le si vous jouez régulièrement aux jeux concernés ; sa désactivation peut imposer un lancement manuel ou un redémarrage.");

        if (ContainsAny(text, RemoteAndVpnApplications))
            return ("Selon votre usage",
                "Accès distant ou réseau privé. Conservez-le seulement si ce service doit être disponible immédiatement après la connexion.");

        if (ContainsAny(text, WindowsConvenienceApplications))
            return ("Selon votre usage",
                "Fonction Windows facultative. Sa désactivation peut retarder les notifications ou les fonctions associées, sans empêcher un lancement manuel.");

        if (!string.IsNullOrWhiteSpace(publisher))
            return ("Selon votre usage",
                $"Programme identifié comme « {publisher} », mais son utilité au démarrage dépend de votre usage. Vérifiez sa description et son emplacement avant de le désactiver.");

        return ("À vérifier",
            "Éditeur et rôle non identifiés. Vérifiez l’emplacement du fichier et sa signature avant toute modification.");
    }

    private static StartupProgramMetadata InspectCommand(string command)
    {
        var path = ExtractExecutablePath(command);
        if (path is null || !File.Exists(path)) return new("", "");
        try
        {
            var version = FileVersionInfo.GetVersionInfo(path);
            return new(version.CompanyName?.Trim() ?? "", version.FileDescription?.Trim() ?? "");
        }
        catch { return new("", ""); }
    }

    public static string? ExtractExecutablePath(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        var expanded = Environment.ExpandEnvironmentVariables(command.Trim());
        if (expanded.StartsWith('"'))
        {
            var closingQuote = expanded.IndexOf('"', 1);
            return closingQuote > 1 ? expanded[1..closingQuote] : null;
        }
        var executable = Regex.Match(expanded, @"^(.+?\.(?:exe|com|bat|cmd|ps1|vbs|lnk))(?=\s|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return executable.Success ? executable.Groups[1].Value.Trim() : expanded.Split(' ', 2)[0];
    }

    private static string Normalize(string value) => value.ToLowerInvariant()
        .Replace('-', ' ').Replace('_', ' ').Replace('.', ' ');

    private static bool ContainsAny(string text, IEnumerable<string> values) => values.Any(text.Contains);

    private static readonly string[] SecurityComponents =
    [
        "securityhealth", "windows security", "windows defender", "defender", "antivirus", "antimalware",
        "bitdefender", "kaspersky", "malwarebytes", "eset", "norton", "mcafee", "avast", "avg antivirus",
        "sophos", "1password", "bitwarden", "keepass", "yubico", "credential", "authenticator"
    ];

    private static readonly string[] DriverComponents =
    [
        "audio driver", "realtek", "touchpad", "synaptics", "elan service", "graphics driver", "display driver",
        "intel graphics", "igfx", "nvidia container", "amd noise suppression", "radeon software startup task",
        "bluetooth", "wireless service", "hotkey service", "dolby", "nahimic", "waves maxxaudio"
    ];

    private static readonly string[] OptionalApplications =
    [
        "steam", "epicgames", "epic games", "battle net", "battlenet", "gog galaxy", "ubisoft connect",
        "riot client", "riotclient", "ea app", "electronic arts", "rockstar games", "playnite", "curseforge",
        "discord", "spotify", "teams", "msteams", "skype", "slack", "zoom", "telegram", "whatsapp",
        "signal desktop", "notion", "obsidian", "adobe", "creative cloud", "ccxprocess", "acrobat assistant",
        "canva", "figma", "vlc", "itunes", "quicktime", "phoneexperiencehost", "copilot"
    ];

    private static readonly string[] UpdateAgents =
    [
        "update", "updater", "update service", "update scheduler", "jusched", "java update", "googleupdate",
        "edgeupdate", "adobe gc invoker", "adobegcinvoker", "launcher", "quick launch", "speed launcher",
        "browser assistant", "office startup", "groove monitor"
    ];

    private static readonly string[] SyncAndBackupApplications =
    [
        "onedrive", "dropbox", "google drive", "googledrivefs", "icloud", "nextcloud", "syncthing",
        "mega sync", "megasync", "pcloud", "backblaze", "acronis", "backup"
    ];

    private static readonly string[] HardwareUtilities =
    [
        "logitech", "lghub", "razer", "corsair", "icue", "steelseries", "glorious core", "roccat",
        "hyperx", "asus armoury", "armoury crate", "msi center", "gigabyte control", "rgb fusion",
        "openrgb", "signalrgb", "fan control", "elgato", "stream deck", "wacom", "huion", "canon",
        "epson", "brother", "hp printer", "lg switch", "calibration studio", "dualcontrol", "dual controller"
    ];

    private static readonly string[] GamingServices =
    [
        "riot vanguard", "vgtray", "easy anti cheat", "easyanticheat", "battleye", "anti cheat",
        "gameguard", "faceit anti cheat", "faceit client"
    ];

    private static readonly string[] RemoteAndVpnApplications =
    [
        "teamviewer", "anydesk", "parsec", "rustdesk", "chrome remote desktop", "tailscale", "zerotier",
        "openvpn", "wireguard", "nordvpn", "proton vpn", "expressvpn"
    ];

    private static readonly string[] WindowsConvenienceApplications =
    [
        "microsoft edge", "msedge", "chrome", "firefox", "opera", "brave", "webex", "cortana",
        "widgets", "powertoys", "xbox app", "game bar"
    ];

    private sealed record StartupProgramMetadata(string Publisher, string Description);
}
