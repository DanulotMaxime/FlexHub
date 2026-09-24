using System.Diagnostics;

namespace PersonalAppsHub.Services;

public sealed record ActiveGameSession(int ProcessId, string Name, DateTime StartedAt, TimeSpan Duration)
{
    public string StartedText => $"Démarré à {StartedAt:HH:mm}";
    public string DurationText => Duration.TotalHours >= 1
        ? $"{(int)Duration.TotalHours} h {Duration.Minutes:00} min"
        : $"{Math.Max(0, (int)Duration.TotalMinutes)} min";
}

public sealed class GameSessionService
{
    public IReadOnlyList<ActiveGameSession> DetectActiveSessions()
    {
        var now = DateTime.Now;
        var sessions = new List<ActiveGameSession>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    var name = process.ProcessName;
                    var recognizableByName = name.Equals("ArmaReforgerSteam", StringComparison.OrdinalIgnoreCase) ||
                                             name.EndsWith("-Win64-Shipping", StringComparison.OrdinalIgnoreCase) ||
                                             name.EndsWith("-WinGDK-Shipping", StringComparison.OrdinalIgnoreCase);
                    if (!recognizableByName && process.MainWindowHandle == IntPtr.Zero) continue;
                    var path = TryGetPath(process);
                    if (!LooksLikeGame(name, path)) continue;
                    var startedAt = process.StartTime;
                    var displayName = process.MainWindowTitle.Trim();
                    if (displayName.Length == 0) displayName = process.MainModule?.FileVersionInfo.ProductName?.Trim() ?? string.Empty;
                    if (displayName.Length == 0) displayName = name;
                    sessions.Add(new ActiveGameSession(process.Id, displayName, startedAt, now - startedAt));
                }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            }
        }
        return sessions.OrderBy(session => session.StartedAt).ToArray();
    }

    private static string TryGetPath(Process process)
    {
        try { return process.MainModule?.FileName?.Replace('/', '\\') ?? string.Empty; }
        catch (InvalidOperationException) { return string.Empty; }
        catch (System.ComponentModel.Win32Exception) { return string.Empty; }
    }

    private static bool LooksLikeGame(string name, string path)
    {
        if (name.Contains("launcher", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("crashreport", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("crashpad", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("bootstrap", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("config", StringComparison.OrdinalIgnoreCase)) return false;
        return name.Equals("ArmaReforgerSteam", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("-Win64-Shipping", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("-WinGDK-Shipping", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("\\steamapps\\common\\", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("\\Epic Games\\", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("\\Riot Games\\", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("\\XboxGames\\", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("\\GOG Galaxy\\Games\\", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("\\EA Games\\", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("\\Ubisoft Game Launcher\\games\\", StringComparison.OrdinalIgnoreCase);
    }
}
