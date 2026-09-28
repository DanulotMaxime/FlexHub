using System.IO;

namespace PersonalAppsHub.Services;

public sealed record FileSearchResult(string Name, string FullPath, string Folder, long SizeBytes, DateTime ModifiedAt)
{
    public string SizeText => TemporaryFileCleanupService.FormatSize(SizeBytes);
    public string ModifiedText => ModifiedAt.ToString("dd/MM/yyyy HH:mm");
    public string TypeText => Path.GetExtension(Name).ToLowerInvariant() switch
    {
        ".pdf" => "Document PDF",
        ".doc" or ".docx" => "Document Word",
        ".xls" or ".xlsx" => "Classeur Excel",
        ".ppt" or ".pptx" => "Présentation",
        ".txt" => "Fichier texte",
        ".jpg" or ".jpeg" => "Image JPEG",
        ".png" => "Image PNG",
        ".gif" => "Image GIF",
        ".webp" => "Image WebP",
        ".mp3" or ".wav" or ".flac" or ".m4a" => "Fichier audio",
        ".mp4" or ".mkv" or ".avi" or ".mov" or ".webm" => "Fichier vidéo",
        ".zip" => "Archive ZIP",
        ".rar" => "Archive RAR",
        ".7z" => "Archive 7-Zip",
        ".exe" => "Application",
        ".msi" => "Programme d’installation",
        ".iso" => "Image disque",
        ".json" or ".xml" or ".yaml" or ".yml" => "Fichier de données",
        ".cs" or ".js" or ".ts" or ".py" or ".html" or ".css" => "Code source",
        var extension when !string.IsNullOrWhiteSpace(extension) => extension.TrimStart('.').ToUpperInvariant(),
        _ => "Sans extension"
    };
}

public sealed record FileSearchSummary(IReadOnlyList<FileSearchResult> Files, int InaccessibleFolders, bool LimitReached);

public sealed class FileFinderService
{
    public Task<FileSearchSummary> SearchAsync(string fragment, CancellationToken cancellationToken) =>
        Task.Run(() => Search(fragment, cancellationToken), cancellationToken);

    private static FileSearchSummary Search(string fragment, CancellationToken cancellationToken)
    {
        var query = fragment.Trim();
        if (query.Length < 2) throw new ArgumentException("Saisissez au moins deux caractères.");
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
        }.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var files = new List<FileSearchResult>();
        var inaccessible = 0;
        var pending = new Stack<string>(roots.Reverse());
        while (pending.Count > 0 && files.Count < 500)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var folder = pending.Pop();
            try
            {
                foreach (var path in Directory.EnumerateFiles(folder))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!Path.GetFileName(path).Contains(query, StringComparison.CurrentCultureIgnoreCase)) continue;
                    try
                    {
                        var info = new FileInfo(path);
                        files.Add(new(info.Name, info.FullName, info.DirectoryName ?? "", info.Length, info.LastWriteTime));
                        if (files.Count >= 500) break;
                    }
                    catch { }
                }
                foreach (var child in Directory.EnumerateDirectories(folder))
                {
                    var attributes = File.GetAttributes(child);
                    if ((attributes & FileAttributes.ReparsePoint) == 0) pending.Push(child);
                }
            }
            catch { inaccessible++; }
        }
        return new(files.OrderBy(file => file.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(), inaccessible, files.Count >= 500);
    }
}
