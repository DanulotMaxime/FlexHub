using System.IO;
using System.Security.Cryptography;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic.FileIO;

namespace PersonalAppsHub.Services;

public sealed class DuplicateFileCandidate : INotifyPropertyChanged
{
    private bool _isSelected;
    public DuplicateFileCandidate(int groupNumber, string path, long sizeBytes, DateTime lastWriteTime)
    {
        GroupNumber = groupNumber; Path = path; SizeBytes = sizeBytes; LastWriteTime = lastWriteTime;
    }
    public int GroupNumber { get; }
    public string Path { get; }
    public long SizeBytes { get; }
    public DateTime LastWriteTime { get; }
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
    }
    public string GroupText => $"Groupe {GroupNumber}";
    public string Name => System.IO.Path.GetFileName(Path);
    public string SizeText => TemporaryFileCleanupService.FormatSize(SizeBytes);
    public string ModifiedText => LastWriteTime.ToString("dd/MM/yyyy HH:mm");
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record DuplicateFileScanResult(IReadOnlyList<DuplicateFileCandidate> Files, int SkippedFiles)
{
    public int GroupCount => Files.Select(file => file.GroupNumber).Distinct().Count();
    public long RecoverableBytes => Files.GroupBy(file => file.GroupNumber)
        .Sum(group => Math.Max(0, group.Count() - 1) * group.First().SizeBytes);
}

public sealed record DuplicateScanProgress(string Phase, int ProcessedFiles, int TotalFiles, bool IsIndeterminate);
public sealed record DuplicateDeleteResult(int DeletedFiles, long RecoveredBytes, int FailedFiles);

public sealed class DuplicateFileService
{
    public async Task<DuplicateFileScanResult> ScanAsync(string root, IProgress<DuplicateScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedRoot = Path.GetFullPath(root);
        var files = new List<FileInfo>();
        var skipped = 0;
        try
        {
            foreach (var path in Directory.EnumerateFiles(normalizedRoot, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            }))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(path);
                    if (info.Exists && info.Length > 0) files.Add(info);
                    if (files.Count % 250 == 0)
                        progress?.Report(new DuplicateScanProgress("Inventaire des fichiers", files.Count, 0, true));
                }
                catch { skipped++; }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { skipped++; }

        var hashed = new List<(FileInfo File, string Hash)>();
        var filesToHash = files.GroupBy(file => file.Length).Where(group => group.Count() > 1)
            .SelectMany(group => group).ToArray();
        progress?.Report(new DuplicateScanProgress("Comparaison SHA-256", 0, filesToHash.Length, false));
        var processedFiles = 0;
        foreach (var file in filesToHash)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read,
                    FileShare.Read, 1024 * 128, FileOptions.Asynchronous | FileOptions.SequentialScan);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
                hashed.Add((file, hash));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { skipped++; }
            finally
            {
                processedFiles++;
                progress?.Report(new DuplicateScanProgress("Comparaison SHA-256", processedFiles, filesToHash.Length, false));
            }
        }

        var duplicates = new List<DuplicateFileCandidate>();
        var groupNumber = 1;
        foreach (var group in hashed.GroupBy(item => (item.File.Length, item.Hash)).Where(group => group.Count() > 1)
                     .OrderByDescending(group => group.Key.Length))
        {
            duplicates.AddRange(group.OrderBy(item => item.File.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(item => new DuplicateFileCandidate(groupNumber, item.File.FullName,
                    item.File.Length, item.File.LastWriteTime)));
            groupNumber++;
        }
        return new DuplicateFileScanResult(duplicates, skipped);
    }

    public DuplicateDeleteResult MoveToRecycleBin(IEnumerable<DuplicateFileCandidate> candidates, string scannedRoot)
    {
        var deleted = 0;
        var failed = 0;
        long recovered = 0;
        foreach (var candidate in candidates)
        {
            try
            {
                if (!TemporaryFileCleanupService.IsWithinRoot(candidate.Path, scannedRoot)) { failed++; continue; }
                var info = new FileInfo(candidate.Path);
                if (!info.Exists) continue;
                var size = info.Length;
                FileSystem.DeleteFile(info.FullName, UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
                deleted++;
                recovered += size;
            }
            catch { failed++; }
        }
        return new DuplicateDeleteResult(deleted, recovered, failed);
    }
}
