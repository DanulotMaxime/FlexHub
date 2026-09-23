using System.IO;

namespace PersonalAppsHub.Services;

public sealed record TemporaryFileCandidate(string Path, long SizeBytes, DateTime LastWriteTime)
{
    public string Name => System.IO.Path.GetFileName(Path);
    public string SizeText => TemporaryFileCleanupService.FormatSize(SizeBytes);
    public string ModifiedText => LastWriteTime.ToString("dd/MM/yyyy HH:mm");
}

public sealed record TemporaryFileScanResult(IReadOnlyList<TemporaryFileCandidate> Files, int SkippedFiles)
{
    public long TotalSizeBytes => Files.Sum(file => file.SizeBytes);
}

public sealed record TemporaryFileDeleteResult(int DeletedFiles, long RecoveredBytes, int FailedFiles);

public sealed class TemporaryFileCleanupService
{
    private readonly string _temporaryRoot = Path.GetFullPath(Path.GetTempPath());

    public Task<TemporaryFileScanResult> ScanAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(cancellationToken), cancellationToken);

    private TemporaryFileScanResult Scan(CancellationToken cancellationToken)
    {
        var files = new List<TemporaryFileCandidate>();
        var skipped = 0;
        IEnumerable<string> paths;
        try
        {
            paths = Directory.EnumerateFiles(_temporaryRoot, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false
            });
        }
        catch { return new TemporaryFileScanResult(files, 1); }

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.LastWriteTime > DateTime.Now.AddHours(-24)) continue;
                files.Add(new TemporaryFileCandidate(info.FullName, info.Length, info.LastWriteTime));
            }
            catch { skipped++; }
        }
        return new TemporaryFileScanResult(files.OrderByDescending(file => file.SizeBytes).ToArray(), skipped);
    }

    public TemporaryFileDeleteResult Delete(IEnumerable<TemporaryFileCandidate> candidates)
    {
        var deleted = 0;
        var failed = 0;
        long recovered = 0;
        foreach (var candidate in candidates)
        {
            try
            {
                if (!IsWithinRoot(candidate.Path, _temporaryRoot)) { failed++; continue; }
                var info = new FileInfo(candidate.Path);
                if (!info.Exists) continue;
                var size = info.Length;
                info.Delete();
                deleted++;
                recovered += size;
            }
            catch { failed++; }
        }
        return new TemporaryFileDeleteResult(deleted, recovered, failed);
    }

    public static bool IsWithinRoot(string path, string root)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedPath = Path.GetFullPath(path);
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatSize(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024d * 1024 * 1024):0.0} Go",
        >= 1024L * 1024 => $"{bytes / (1024d * 1024):0.0} Mo",
        >= 1024L => $"{bytes / 1024d:0.0} Ko",
        _ => $"{bytes} o"
    };
}
