using Microsoft.Win32;
using System.IO;

namespace PersonalAppsHub.Services;

public sealed record StartupEntry(string Name, string Command, string Source, string Scope, string Status,
    string Recommendation, string Reason, RegistryHive Hive, RegistryView View, string ApprovalPath, string ApprovalName)
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
                    var (recommendation, reason) = Analyze(name, command);
                    entries.Add(new StartupEntry(name, command, source, scope, ReadApprovalStatus(approved, name),
                        recommendation, reason, hive, view, ApprovedRunPath, name));
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
                var (recommendation, reason) = Analyze(name, path);
                entries.Add(new StartupEntry(name, path, source, scope, ReadApprovalStatus(approved, approvalName),
                    recommendation, reason, approvalHive, RegistryView.Default, ApprovedFolderPath, approvalName));
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

    public static (string Recommendation, string Reason) Analyze(string name, string command)
    {
        var text = $"{name} {command}".ToLowerInvariant();
        var important = new[] { "securityhealth", "windows defender", "antivirus", "audio driver", "realtek", "touchpad", "synaptics", "onedrive", "flexhub" };
        if (important.Any(text.Contains))
            return ("À conserver", "Sécurité, pilote, synchronisation ou fonction explicitement configurée.");

        var optional = new[] { "update", "updater", "launcher", "steam", "epicgames", "discord", "spotify", "teams", "skype", "adobe", "java", "edge", "chrome" };
        if (optional.Any(text.Contains))
            return ("Désactivation envisageable", "Application non essentielle à Windows ; elle pourra toujours être lancée manuellement.");

        return ("À vérifier", "Rôle insuffisamment clair : vérifiez l’éditeur et votre usage avant toute modification.");
    }
}
