using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace PersonalAppsHub.Services;

public static class ElevatedLaunchService
{
    public const string ElevatedArgument = "--elevated-task";
    public const string InstallArgument = "--install-elevated-launch";
    private const string TaskName = "FlexHub Elevated";
    private static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PersonalAppsHub", "elevated-launch.json");

    public static bool IsElevated => new WindowsPrincipal(WindowsIdentity.GetCurrent())
        .IsInRole(WindowsBuiltInRole.Administrator);

    public static bool HasArgument(IEnumerable<string> arguments, string expected) =>
        arguments.Any(argument => string.Equals(argument, expected, StringComparison.OrdinalIgnoreCase));

    public static bool ShouldOfferSetup(string executablePath)
    {
        var state = LoadState();
        return state is null || !string.Equals(state.ExecutablePath, executablePath, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldUseScheduledTask(string executablePath)
    {
        var state = LoadState();
        return state is { Enabled: true } &&
               string.Equals(state.ExecutablePath, executablePath, StringComparison.OrdinalIgnoreCase);
    }

    public static void RememberDeclined(string executablePath) =>
        SaveState(new ElevatedLaunchState(executablePath, false));

    public static int RequestInstallation(string executablePath)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = InstallArgument,
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory
        });
        if (process is null) return -1;
        process.WaitForExit();
        return process.ExitCode;
    }

    public static void InstallForCurrentUser(string executablePath)
    {
        if (!IsElevated) throw new InvalidOperationException("L’installation du lanceur nécessite les droits administrateur.");
        executablePath = Path.GetFullPath(executablePath);
        var workingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory;
        var user = WindowsIdentity.GetCurrent().Name;
        var script = $"$ErrorActionPreference='Stop';" +
            $"$a=New-ScheduledTaskAction -Execute '{QuotePowerShell(executablePath)}' -Argument '{ElevatedArgument}' -WorkingDirectory '{QuotePowerShell(workingDirectory)}';" +
            $"$p=New-ScheduledTaskPrincipal -UserId '{QuotePowerShell(user)}' -LogonType Interactive -RunLevel Highest;" +
            "$s=New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -MultipleInstances IgnoreNew;" +
            $"Register-ScheduledTask -TaskName '{TaskName}' -Action $a -Principal $p -Settings $s -Force | Out-Null";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Le Planificateur de tâches n’a pas pu être lancé.");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"La création du lanceur administrateur a échoué (code {process.ExitCode}).");
        InstallPawnIoIfNeeded();
        SaveState(new ElevatedLaunchState(executablePath, true));
        AppLog.Write("LANCEUR ADMINISTRATEUR | Tâche sécurisée enregistrée pour l’utilisateur courant.");
    }

    private static void InstallPawnIoIfNeeded()
    {
        var pawnIo = LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled
            ? LibreHardwareMonitor.PawnIo.PawnIo.Version
            : null;
        if (pawnIo is not null && pawnIo >= new Version(2, 2, 0, 0)) return;
        var installer = Path.Combine(AppContext.BaseDirectory, "Tools", "PawnIO_setup.exe");
        if (!File.Exists(installer))
            throw new FileNotFoundException("L’installateur signé PawnIO est absent du paquet FlexHub.", installer);
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = installer,
            Arguments = "-install -silent",
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("L’installation du pilote PawnIO n’a pas pu démarrer.");
        process.WaitForExit();
        if (process.ExitCode is not 0 and not 3010)
            throw new InvalidOperationException($"L’installation du pilote PawnIO a échoué (code {process.ExitCode}).");
        AppLog.Write(process.ExitCode == 3010
            ? "CAPTEURS CPU | Pilote PawnIO installé; redémarrage Windows requis."
            : "CAPTEURS CPU | Pilote PawnIO signé installé.");
    }

    public static bool RunScheduledTask()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = $"/Run /TN \"{TaskName}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });
        if (process is null) return false;
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    private static string QuotePowerShell(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static ElevatedLaunchState? LoadState()
    {
        try
        {
            return File.Exists(StatePath)
                ? JsonSerializer.Deserialize<ElevatedLaunchState>(File.ReadAllText(StatePath))
                : null;
        }
        catch { return null; }
    }

    private static void SaveState(ElevatedLaunchState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        var temporary = StatePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state));
        File.Move(temporary, StatePath, true);
    }

    private sealed record ElevatedLaunchState(string ExecutablePath, bool Enabled);
}
