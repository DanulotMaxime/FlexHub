using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace PersonalAppsHub.Services;

public sealed class DownloadMoveCandidate : INotifyPropertyChanged
{
    private bool _isSelected = true;
    public DownloadMoveCandidate(string sourcePath, string category, string destinationPath, long sizeBytes, DateTime modifiedAt)
    {
        SourcePath = sourcePath; Category = category; DestinationPath = destinationPath;
        SizeBytes = sizeBytes; ModifiedAt = modifiedAt;
    }
    public string SourcePath { get; }
    public string Category { get; }
    public string DestinationPath { get; }
    public long SizeBytes { get; }
    public DateTime ModifiedAt { get; }
    public string Name => Path.GetFileName(SourcePath);
    public string DestinationFolder => Path.GetFileName(Path.GetDirectoryName(DestinationPath)) ?? "Autres";
    public string SizeText => TemporaryFileCleanupService.FormatSize(SizeBytes);
    public string ModifiedText => ModifiedAt.ToString("dd/MM/yyyy HH:mm");
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record DownloadsScanResult(IReadOnlyList<DownloadMoveCandidate> Files, int SkippedFiles);
public sealed record DownloadsMoveResult(int MovedFiles, int FailedFiles);

public sealed class DownloadsOrganizerService
{
    public Task<DownloadsScanResult> ScanAsync(string downloadsRoot, TimeSpan? minimumAge = null) =>
        Task.Run(() => Scan(downloadsRoot, minimumAge ?? TimeSpan.FromMinutes(10)));

    private static DownloadsScanResult Scan(string downloadsRoot, TimeSpan minimumAge)
    {
        var root = Path.GetFullPath(downloadsRoot);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Le dossier Téléchargements est introuvable.");
        var files = new List<DownloadMoveCandidate>();
        var skipped = 0;
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.LastWriteTime > DateTime.Now.Subtract(minimumAge) || IsPartialDownload(info.Extension)) continue;
            var category = CategoryFor(info.Extension);
            var destination = UniqueDestination(Path.Combine(root, category, info.Name));
            files.Add(new DownloadMoveCandidate(info.FullName, category, destination, info.Length, info.LastWriteTime));
        }
        catch { skipped++; }
        return new DownloadsScanResult(files.OrderBy(file => file.Category)
            .ThenBy(file => file.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(), skipped);
    }

    public DownloadsMoveResult Move(IEnumerable<DownloadMoveCandidate> candidates, string downloadsRoot)
    {
        var root = Path.GetFullPath(downloadsRoot);
        var moved = 0;
        var failed = 0;
        foreach (var candidate in candidates)
        try
        {
            if (!TemporaryFileCleanupService.IsWithinRoot(candidate.SourcePath, root) ||
                !TemporaryFileCleanupService.IsWithinRoot(candidate.DestinationPath, root) ||
                !File.Exists(candidate.SourcePath)) { failed++; continue; }
            var destination = UniqueDestination(candidate.DestinationPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Move(candidate.SourcePath, destination);
            moved++;
        }
        catch { failed++; }
        return new DownloadsMoveResult(moved, failed);
    }

    private static string CategoryFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" or ".svg" or ".heic" => "Images",
        ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" => "Vidéos",
        ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" or ".m4a" => "Musique",
        ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" or ".txt" or ".odt" => "Documents",
        ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "Archives",
        ".exe" or ".msi" or ".msix" or ".appx" => "Installateurs",
        ".iso" or ".img" => "Images disque",
        ".cs" or ".js" or ".ts" or ".py" or ".json" or ".xml" or ".html" or ".css" => "Code",
        _ => "Autres"
    };

    private static bool IsPartialDownload(string extension) => extension.ToLowerInvariant() is ".crdownload" or ".part" or ".partial" or ".tmp";

    private static string UniqueDestination(string path)
    {
        if (!File.Exists(path)) return path;
        var directory = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        for (var index = 2; index < 10_000; index++)
        {
            var candidate = Path.Combine(directory, $"{name} ({index}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }
        throw new IOException("Impossible de créer un nom de fichier unique.");
    }
}
