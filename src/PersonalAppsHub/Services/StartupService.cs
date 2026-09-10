using Microsoft.Win32;

namespace PersonalAppsHub.Services;

public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "FlexHub";
    private const string LegacyValueName = "PersonalAppsHub";

    public static void SetEnabled(bool enabled)
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("La clé de démarrage de Windows est inaccessible.");

        if (!enabled)
        {
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
            runKey.DeleteValue(LegacyValueName, throwOnMissingValue: false);
            return;
        }

        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Le chemin de l'application est introuvable.");
        runKey.SetValue(ValueName, $"\"{executablePath}\"", RegistryValueKind.String);
        runKey.DeleteValue(LegacyValueName, throwOnMissingValue: false);
    }
}
