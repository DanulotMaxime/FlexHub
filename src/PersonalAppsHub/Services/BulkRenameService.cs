using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace PersonalAppsHub.Services;

public sealed class RenameCandidate : INotifyPropertyChanged
{
    private bool _isSelected = true;
    public RenameCandidate(string sourcePath, string destinationPath, DateTime date, string dateSource)
    {
        SourcePath = sourcePath; DestinationPath = destinationPath; Date = date; DateSource = dateSource;
    }
    public string SourcePath { get; }
    public string DestinationPath { get; }
    public DateTime Date { get; }
    public string DateSource { get; }
    public string OldName => Path.GetFileName(SourcePath);
    public string NewName => Path.GetFileName(DestinationPath);
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new(nameof(IsSelected))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record BulkRenameResult(int RenamedFiles, int FailedFiles);

public sealed class BulkRenameService
{
    public Task<IReadOnlyList<RenameCandidate>> PreviewAsync(string folder, string prefix, bool includeDate,
        bool includeNumber, bool smartName) => Task.Run(() => Preview(folder, prefix, includeDate, includeNumber, smartName));

    private static IReadOnlyList<RenameCandidate> Preview(string folder, string prefix, bool includeDate, bool includeNumber, bool smartName)
    {
        var root = Path.GetFullPath(folder);
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("Le dossier choisi est introuvable.");
        var safePrefix = Sanitize(prefix.Trim());
        if (string.IsNullOrWhiteSpace(safePrefix) && !includeDate && !includeNumber && !smartName)
            throw new InvalidOperationException("Ajoutez un préfixe, une date ou une numérotation.");
        var paths = Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase).ToArray();
        var candidates = new List<RenameCandidate>();
        for (var index = 0; index < paths.Length; index++)
        {
            var path = paths[index];
            var (date, source) = ReadBestDate(path);
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(safePrefix)) parts.Add(safePrefix);
            var analysis = smartName ? AnalyzeName(path) : null;
            if (smartName && !analysis.HasValue) continue;
            if (analysis.HasValue) { parts.Add(analysis.Value.Name); source = analysis.Value.Source; }
            if (includeDate) parts.Add(date.ToString("yyyy-MM-dd_HH-mm-ss"));
            if (includeNumber) parts.Add((index + 1).ToString($"D{Math.Max(3, paths.Length.ToString().Length)}"));
            if (parts.Count == 0) continue;
            var newName = string.Join("_", parts) + Path.GetExtension(path).ToLowerInvariant();
            var destination = Path.Combine(root, newName);
            if (!string.Equals(path, destination, StringComparison.OrdinalIgnoreCase))
                candidates.Add(new RenameCandidate(path, destination, date, source));
        }
        return candidates;
    }

    public BulkRenameResult Rename(IEnumerable<RenameCandidate> candidates, string folder)
    {
        var root = Path.GetFullPath(folder);
        var selected = candidates.ToArray();
        var duplicateDestination = selected.GroupBy(item => item.DestinationPath, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1);
        if (duplicateDestination) throw new InvalidOperationException("Plusieurs fichiers auraient le même nouveau nom.");
        var renamed = 0; var failed = 0;
        foreach (var candidate in selected)
        try
        {
            if (!TemporaryFileCleanupService.IsWithinRoot(candidate.SourcePath, root) ||
                !TemporaryFileCleanupService.IsWithinRoot(candidate.DestinationPath, root) ||
                !File.Exists(candidate.SourcePath) || File.Exists(candidate.DestinationPath)) { failed++; continue; }
            File.Move(candidate.SourcePath, candidate.DestinationPath);
            renamed++;
        }
        catch { failed++; }
        return new BulkRenameResult(renamed, failed);
    }

    private static (DateTime Date, string Source) ReadBestDate(string path)
    {
        if (Path.GetExtension(path).ToLowerInvariant() is ".jpg" or ".jpeg" or ".tif" or ".tiff")
        try
        {
            using var image = Image.FromFile(path);
            var property = image.PropertyItems.FirstOrDefault(item => item.Id == 0x9003 || item.Id == 0x0132);
            if (property is not null)
            {
                var text = Encoding.ASCII.GetString(property.Value ?? Array.Empty<byte>()).Trim('\0');
                if (DateTime.TryParseExact(text, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var exifDate)) return (exifDate, "EXIF");
            }
        }
        catch { }
        return (File.GetLastWriteTime(path), "Date du fichier");
    }

    private static string Sanitize(string value)
    {
        foreach (var character in Path.GetInvalidFileNameChars()) value = value.Replace(character, '-');
        return value.Trim().TrimEnd('.');
    }

    private static (string Name, string Source)? AnalyzeName(string path)
    {
        try
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension is ".txt" or ".md" or ".csv" or ".json" or ".xml" or ".cs" or ".js" or ".ts" or ".py" or ".html" or ".css")
            {
                using var reader = new StreamReader(path, Encoding.UTF8, true);
                for (var index = 0; index < 30 && !reader.EndOfStream; index++)
                {
                    var line = reader.ReadLine()?.Trim().TrimStart('#', '/', '*', '-', ' ');
                    if (line?.Length >= 4) return (SmartFragment(line), "Contenu texte");
                }
            }
            if (extension == ".docx")
            {
                using var archive = ZipFile.OpenRead(path);
                var entry = archive.GetEntry("docProps/core.xml") ?? archive.GetEntry("word/document.xml");
                if (entry is not null)
                {
                    using var reader = new StreamReader(entry.Open());
                    var text = Regex.Replace(reader.ReadToEnd(), "<[^>]+>", " ");
                    text = System.Net.WebUtility.HtmlDecode(Regex.Replace(text, @"\s+", " ")).Trim();
                    if (text.Length >= 4) return (SmartFragment(text), "Contenu DOCX");
                }
            }
            if (extension == ".pdf")
            {
                var bytes = File.ReadAllBytes(path);
                var head = Encoding.Latin1.GetString(bytes, 0, Math.Min(bytes.Length, 1_000_000));
                var title = Regex.Match(head, @"/Title\s*\((?<value>(?:\\.|[^)])+)\)").Groups["value"].Value;
                if (title.Length >= 4) return (SmartFragment(title), "Titre PDF");
            }
            if (extension == ".mp3")
            {
                using var stream = File.OpenRead(path);
                if (stream.Length >= 128)
                {
                    stream.Seek(-128, SeekOrigin.End); var tag = new byte[128]; stream.ReadExactly(tag);
                    if (Encoding.Latin1.GetString(tag, 0, 3) == "TAG")
                    {
                        var title = Encoding.Latin1.GetString(tag, 3, 30).Trim('\0', ' ');
                        var artist = Encoding.Latin1.GetString(tag, 33, 30).Trim('\0', ' ');
                        if (title.Length > 0) return (SmartFragment($"{artist} {title}"), "Tags audio");
                    }
                }
            }
            if (extension is ".jpg" or ".jpeg" or ".tif" or ".tiff")
            {
                using var image = Image.FromFile(path);
                var model = image.PropertyItems.FirstOrDefault(item => item.Id == 0x0110);
                var text = model is null ? "Photo" : Encoding.ASCII.GetString(model.Value ?? []).Trim('\0', ' ');
                return (SmartFragment(text), "Métadonnées EXIF");
            }
        }
        catch { }
        return null;
    }

    private static string SmartFragment(string value)
    {
        var cleaned = Regex.Replace(System.Net.WebUtility.HtmlDecode(value), @"[^\p{L}\p{N}]+", "_").Trim('_');
        if (cleaned.Length > 60) cleaned = cleaned[..60].TrimEnd('_');
        return Sanitize(cleaned);
    }

}
