using System.IO;

namespace PersonalAppsHub.Services;

public sealed record TemporaryFileCandidate(string Path, long SizeBytes, DateTime LastWriteTime,
    string Category = "Temporaire Windows", string AllowedRoot = "")
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

    public Task<TemporaryFileScanResult> ScanWindowsTemporaryAsync(TimeSpan minimumAge,
        CancellationToken cancellationToken = default) => Task.Run(() =>
            ScanSources([new CleanupSource("Temporaire Windows", _temporaryRoot, minimumAge)], cancellationToken), cancellationToken);

    private TemporaryFileScanResult Scan(CancellationToken cancellationToken) =>
        ScanSources(GetScanSources(), cancellationToken);

    private static TemporaryFileScanResult ScanSources(IEnumerable<CleanupSource> sources, CancellationToken cancellationToken)
    {
        var files = new List<TemporaryFileCandidate>();
        var skipped = 0;
        foreach (var source in sources)
        {
            IEnumerable<string> paths;
            try { paths = EnumerateFiles(source.Root); }
            catch { skipped++; continue; }
            foreach (var path in paths)
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = new FileInfo(path);
                if (!info.Exists || info.LastWriteTime > DateTime.Now - source.MinimumAge) continue;
                files.Add(new TemporaryFileCandidate(info.FullName, info.Length, info.LastWriteTime,
                    source.Category, source.Root));
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
                var allowedRoot = string.IsNullOrWhiteSpace(candidate.AllowedRoot) ? _temporaryRoot : candidate.AllowedRoot;
                if (!IsWithinRoot(candidate.Path, allowedRoot)) { failed++; continue; }
                var info = new FileInfo(candidate.Path);
                if (!info.Exists) continue;
                var size = info.Length;
                var originalAttributes = info.Attributes;
                var readOnlyRemoved = (originalAttributes & FileAttributes.ReadOnly) != 0;
                if (readOnlyRemoved) info.Attributes = originalAttributes & ~FileAttributes.ReadOnly;
                try
                {
                    info.Delete();
                }
                catch
                {
                    if (readOnlyRemoved && info.Exists)
                    {
                        try { info.Attributes = originalAttributes; }
                        catch { }
                    }
                    throw;
                }
                deleted++;
                recovered += size;
            }
            catch { failed++; }
        }
        return new TemporaryFileDeleteResult(deleted, recovered, failed);
    }

    private IEnumerable<CleanupSource> GetScanSources()
    {
        yield return new CleanupSource("Temporaire Windows", _temporaryRoot, TimeSpan.FromHours(24));
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        foreach (var source in ExistingSources(local, "Google Chrome", @"Google\Chrome\User Data", "Cache", "Code Cache", "GPUCache")) yield return source;
        foreach (var source in ExistingSources(local, "Microsoft Edge", @"Microsoft\Edge\User Data", "Cache", "Code Cache", "GPUCache")) yield return source;
        foreach (var source in ExistingSources(local, "Mozilla Firefox", @"Mozilla\Firefox\Profiles", "cache2")) yield return source;
        foreach (var source in ExistingSources(local, "Discord", "Discord", "Cache", "Code Cache", "GPUCache")) yield return source;
        foreach (var source in ExistingSources(local, "Steam", "Steam", "htmlcache")) yield return source;
        foreach (var source in ExistingSources(local, "Visual Studio", @"Microsoft\VisualStudio", "ComponentModelCache")) yield return source;
    }

    private static IEnumerable<CleanupSource> ExistingSources(string local, string category, string parentRelative, params string[] cacheNames)
    {
        var parent = Path.Combine(local, parentRelative);
        if (!Directory.Exists(parent)) yield break;
        var wanted = cacheNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(parent, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            });
        }
        catch { yield break; }
        foreach (var directory in directories)
            if (wanted.Contains(Path.GetFileName(directory)))
                yield return new CleanupSource(category, Path.GetFullPath(directory), TimeSpan.FromDays(7));
    }

    private static IEnumerable<string> EnumerateFiles(string root) => Directory.EnumerateFiles(root, "*", new EnumerationOptions
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        ReturnSpecialDirectories = false,
        AttributesToSkip = FileAttributes.ReparsePoint
    });

    private sealed record CleanupSource(string Category, string Root, TimeSpan MinimumAge);

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
