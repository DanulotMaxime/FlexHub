using System.IO;
using System.Diagnostics;
using System.Reflection;

namespace PersonalAppsHub.Services;

public static class AppLog
{
    private static readonly object Sync = new();
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PersonalAppsHub");
    public static string PathName => Path.Combine(Folder, "flexhub.log");
    private static string LegacyPath => Path.Combine(Folder, "corrector.log");

    public static void Write(string message)
    {
        lock (Sync)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                if (!File.Exists(PathName) && File.Exists(LegacyPath))
                    File.Move(LegacyPath, PathName);
                if (File.Exists(PathName) && new FileInfo(PathName).Length > 2 * 1024 * 1024)
                {
                    var previous = Path.Combine(Folder, "flexhub.previous.log");
                    File.Move(PathName, previous, overwrite: true);
                }
                File.AppendAllText(PathName, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {message}{Environment.NewLine}");
            }
            catch { }
        }
    }

    public static void WriteException(string context, Exception exception)
    {
        Write($"{context} | {MemorySnapshot()} | {exception}");
    }

    public static string MemorySnapshot()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "inconnue";
            return $"Version={version}; PID={process.Id}; WorkingSet={ToMiB(process.WorkingSet64)} MiB; " +
                   $"Privée={ToMiB(process.PrivateMemorySize64)} MiB; Managée={ToMiB(GC.GetTotalMemory(false))} MiB; " +
                   $"Handles={process.HandleCount}; Threads={process.Threads.Count}; GC=({GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)})";
        }
        catch { return "Diagnostic mémoire indisponible"; }
    }

    private static long ToMiB(long bytes) => bytes / (1024 * 1024);
}
