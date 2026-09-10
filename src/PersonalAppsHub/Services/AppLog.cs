using System.IO;

namespace PersonalAppsHub.Services;

public static class AppLog
{
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PersonalAppsHub");
    public static string PathName => Path.Combine(Folder, "corrector.log");

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            if (File.Exists(PathName) && new FileInfo(PathName).Length > 2 * 1024 * 1024)
            {
                var previous = Path.Combine(Folder, "corrector.previous.log");
                File.Move(PathName, previous, overwrite: true);
            }
            File.AppendAllText(PathName, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {message}{Environment.NewLine}");
        }
        catch { }
    }
}
