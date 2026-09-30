using System.Diagnostics;
using System.IO;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace PersonalAppsHub.Services;

public sealed class DailyActivityReportService : IDisposable
{
    private readonly object _sync = new();
    private readonly Dictionary<string, TimeSpan> _applicationDurations = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _modifiedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, string> _processNames = new();
    private readonly List<FileSystemWatcher> _watchers = new();
    private DateTime _lastSample = DateTime.Now;
    private DateTime _lastForegroundProbe = DateTime.MinValue;
    private DateTime _trackingDate = DateTime.Today;
    private DateTime _lastSnapshotSave = DateTime.MinValue;
    private string? _lastApplication;

    public void SampleForegroundApplication()
    {
        var now = DateTime.Now;
        lock (_sync)
        {
            if (_trackingDate != now.Date)
            {
                TrySaveSnapshot(CreateSnapshot(_trackingDate, now, GetTrackedFiles()));
                _applicationDurations.Clear();
                _trackingDate = now.Date;
                _lastApplication = null;
                _modifiedFiles.Clear();
            }
            if (now - _lastForegroundProbe < TimeSpan.FromSeconds(20)) return;
            if (_lastApplication != null)
            {
                var elapsed = now - _lastSample;
                if (elapsed > TimeSpan.Zero && elapsed < TimeSpan.FromMinutes(1))
                    _applicationDurations[_lastApplication] = _applicationDurations.GetValueOrDefault(_lastApplication) + elapsed;
            }

            _lastSample = now;
            _lastForegroundProbe = now;
            _lastApplication = ReadForegroundApplication();
            if (now - _lastSnapshotSave >= TimeSpan.FromMinutes(2))
            {
                TrySaveSnapshot(CreateSnapshot(now.Date, now, GetTrackedFiles()));
                _lastSnapshotSave = now;
            }
        }
    }

    public void StartMonitoring()
    {
        if (_watchers.Count > 0) return;
        RestoreTodaySnapshot();
        foreach (var folder in PersonalFolders())
        {
            try
            {
                var watcher = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    Filter = "*",
                    InternalBufferSize = 8192
                };
                watcher.Created += OnFileChanged;
                watcher.Changed += OnFileChanged;
                watcher.Renamed += (_, args) => RememberFile(args.FullPath);
                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
            catch { }
        }
        _lastSample = DateTime.Now;
        _lastForegroundProbe = DateTime.MinValue;
    }

    public void StopMonitoring()
    {
        lock (_sync)
        {
            if (_applicationDurations.Count > 0 || _modifiedFiles.Count > 0)
                TrySaveSnapshot(CreateSnapshot(_trackingDate, DateTime.Now, GetTrackedFiles()));
        }
        foreach (var watcher in _watchers) watcher.Dispose();
        _watchers.Clear();
        _lastApplication = null;
    }

    public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
    {
        await Task.Run(SampleForegroundApplication, cancellationToken);
        var today = DateTime.Today;
        var files = GetTrackedFiles();
        var report = BuildReport(today, files);
        var folder = ReportsFolder;
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, $"rapport-{today:yyyy-MM-dd}.txt"), report, cancellationToken);
        DailyActivitySnapshot snapshot;
        lock (_sync) snapshot = CreateSnapshot(today, DateTime.Now, files);
        await SaveSnapshotAsync(snapshot, cancellationToken);
        return report;
    }

    public string BuildWeeklyStatistics()
    {
        var today = DateTime.Today;
        SampleForegroundApplication();
        DailyActivitySnapshot liveToday;
        lock (_sync) liveToday = CreateSnapshot(today, DateTime.Now, GetTrackedFiles());
        var snapshots = LoadSnapshots().Where(item => item.Date.Date != today).Append(liveToday).ToArray();
        var current = snapshots.Where(item => item.Date.Date >= today.AddDays(-6) && item.Date.Date <= today).ToArray();
        var previous = snapshots.Where(item => item.Date.Date >= today.AddDays(-13) && item.Date.Date < today.AddDays(-6)).ToArray();
        var currentSeconds = current.Sum(item => item.ApplicationSeconds.Values.Sum());
        var previousSeconds = previous.Sum(item => item.ApplicationSeconds.Values.Sum());
        var currentFiles = current.Sum(item => item.ModifiedFileCount);
        var previousFiles = previous.Sum(item => item.ModifiedFileCount);
        var builder = new StringBuilder();
        builder.AppendLine("STATISTIQUES DES 7 DERNIERS JOURS");
        builder.AppendLine($"Du {today.AddDays(-6):dd/MM} au {today:dd/MM/yyyy}");
        builder.AppendLine("────────────────────────────────────────");
        builder.AppendLine($"Jours enregistrés : {current.Length}/7");
        builder.AppendLine($"Temps d’utilisation mesuré : {FormatDuration(TimeSpan.FromSeconds(currentSeconds))}");
        builder.AppendLine($"Fichiers personnels modifiés : {currentFiles}");
        builder.AppendLine();
        builder.AppendLine("ACTIVITÉ PAR JOUR");
        var maxSeconds = Math.Max(1, current.Select(item => item.ApplicationSeconds.Values.Sum()).DefaultIfEmpty().Max());
        foreach (var day in Enumerable.Range(0, 7).Select(offset => today.AddDays(-6 + offset)))
        {
            var snapshot = current.FirstOrDefault(item => item.Date.Date == day);
            var seconds = snapshot?.ApplicationSeconds.Values.Sum() ?? 0;
            var bars = new string('█', (int)Math.Round(seconds / maxSeconds * 18));
            builder.AppendLine($"{day:ddd dd/MM}  {bars,-18} {FormatDuration(TimeSpan.FromSeconds(seconds))}");
        }
        builder.AppendLine().AppendLine("APPLICATIONS LES PLUS UTILISÉES");
        var applications = current.SelectMany(item => item.ApplicationSeconds)
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new { Name = group.Key, Seconds = group.Sum(item => item.Value) })
            .OrderByDescending(item => item.Seconds).Take(10).ToArray();
        if (applications.Length == 0) builder.AppendLine("Pas encore assez de données.");
        foreach (var application in applications)
            builder.AppendLine($"• {application.Name} — {FormatDuration(TimeSpan.FromSeconds(application.Seconds))}");
        builder.AppendLine().AppendLine("COMPARAISON AVEC LA SEMAINE PRÉCÉDENTE");
        builder.AppendLine(previous.Length == 0
            ? "Aucune semaine précédente disponible pour le moment."
            : $"Temps mesuré : {FormatChange(currentSeconds, previousSeconds)}\nFichiers modifiés : {FormatChange(currentFiles, previousFiles)}");
        builder.AppendLine().AppendLine("Ces chiffres décrivent l’utilisation du PC ; ils ne constituent pas une note de productivité.");
        return builder.ToString();
    }

    public ActivityStatistics GetStatistics(int days = 7)
    {
        days = Math.Clamp(days, 1, 3650);
        SampleForegroundApplication();
        var today = DateTime.Today;
        DailyActivitySnapshot liveToday;
        lock (_sync) liveToday = CreateSnapshot(today, DateTime.Now, GetTrackedFiles());
        var snapshots = LoadSnapshots().Where(item => item.Date.Date != today).Append(liveToday)
            .GroupBy(item => item.Date.Date).Select(group => group.OrderByDescending(item => item.GeneratedAt).First())
            .ToArray();
        return CalculateStatistics(snapshots, today, days);
    }

    public static ActivityStatistics CalculateStatistics(IEnumerable<DailyActivitySnapshot> snapshots,
        DateTime today, int days = 7)
    {
        days = Math.Clamp(days, 1, 3650);
        today = today.Date;
        var distinctSnapshots = snapshots.GroupBy(item => item.Date.Date)
            .Select(group => group.OrderByDescending(item => item.GeneratedAt).First()).ToArray();
        var start = today.AddDays(-(days - 1));
        var selected = distinctSnapshots.Where(item => item.Date.Date >= start && item.Date.Date <= today).ToArray();
        var totals = Enumerable.Range(0, days).Select(offset => start.AddDays(offset)).Select(date =>
        {
            var snapshot = selected.FirstOrDefault(item => item.Date.Date == date);
            return new ActivityDayStatistic(date, snapshot?.ApplicationSeconds.Values.Sum() ?? 0,
                snapshot?.ModifiedFileCount ?? 0);
        }).ToArray();
        var topApplications = selected.SelectMany(item => item.ApplicationSeconds)
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ActivityApplicationStatistic(group.Key, group.Sum(item => item.Value)))
            .OrderByDescending(item => item.Seconds).Take(10).ToArray();
        var files = selected.SelectMany(item => item.ModifiedFiles ?? [])
            .GroupBy(item => item.FullPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.ModifiedAt).First())
            .OrderByDescending(item => item.ModifiedAt).Take(100).ToArray();
        return new ActivityStatistics(totals, topApplications, files,
            distinctSnapshots.Length == 0 ? null : distinctSnapshots.Min(item => item.Date));
    }

    public string? LoadTodayReport()
    {
        var path = Path.Combine(ReportsFolder, $"rapport-{DateTime.Today:yyyy-MM-dd}.txt");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private string BuildReport(DateTime date, IReadOnlyList<FileInfo> files)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"RAPPORT DU {date:dd/MM/yyyy}");
        builder.AppendLine($"Mis à jour à {DateTime.Now:HH:mm}");
        builder.AppendLine("────────────────────────────────────────");
        builder.AppendLine();
        builder.AppendLine("TEMPS PASSÉ DANS LES APPLICATIONS");
        var applications = _applicationDurations.Where(item => item.Value >= TimeSpan.FromSeconds(10))
            .OrderByDescending(item => item.Value).Take(30).ToArray();
        if (applications.Length == 0) builder.AppendLine("Pas encore assez de données. Laissez FlexHub fonctionner quelques minutes.");
        foreach (var item in applications)
            builder.AppendLine($"• {item.Key} — {FormatDuration(item.Value)}");

        builder.AppendLine().AppendLine("FICHIERS MODIFIÉS");
        if (files.Count == 0) builder.AppendLine("Aucun document personnel modifié aujourd’hui.");
        else
        {
            builder.AppendLine($"{files.Count} fichier(s) personnel(s) modifié(s) aujourd’hui.");
            foreach (var group in files.GroupBy(GetFolderLabel).OrderByDescending(group => group.Count()))
                builder.AppendLine($"• {group.Key} : {group.Count()} fichier(s)");

            builder.AppendLine().AppendLine("LES 20 PLUS RÉCENTS");
            foreach (var file in files.Take(20))
                builder.AppendLine($"• {file.LastWriteTime:HH:mm}  {file.Name}  [{GetFolderLabel(file)}]");
            if (files.Count > 20) builder.AppendLine($"… et {files.Count - 20} autre(s) fichier(s).");
        }
        builder.AppendLine().AppendLine("Confidentialité : rapport créé et conservé uniquement sur ce PC.");
        return builder.ToString();
    }

    private static IEnumerable<string> PersonalFolders()
    {
        return new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
        }.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs args) => RememberFile(args.FullPath);

    private void RememberFile(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (file.Exists && IsUsefulPersonalFile(file)) _modifiedFiles[path] = DateTime.Now;
        }
        catch { }
    }

    private List<FileInfo> GetTrackedFiles()
    {
        return _modifiedFiles.OrderByDescending(item => item.Value).Select(item =>
        {
            try { return new FileInfo(item.Key); }
            catch { return null; }
        }).Where(file => file?.Exists == true).Cast<FileInfo>().ToList();
    }

    private DailyActivitySnapshot CreateSnapshot(DateTime date, DateTime generatedAt, IReadOnlyList<FileInfo> files) =>
        new(date.Date, generatedAt,
            _applicationDurations.ToDictionary(item => item.Key, item => item.Value.TotalSeconds, StringComparer.OrdinalIgnoreCase),
            files.Count,
            files.Select(file => new ActivityFileSnapshot(file.FullName,
                _modifiedFiles.TryGetValue(file.FullName, out var changedAt) ? changedAt : file.LastWriteTime)).ToList());

    private static void SaveSnapshot(DailyActivitySnapshot snapshot)
    {
        Directory.CreateDirectory(ReportsFolder);
        var path = Path.Combine(ReportsFolder, $"rapport-{snapshot.Date:yyyy-MM-dd}.json");
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(snapshot, JsonOptions));
        File.Move(temporary, path, overwrite: true);
    }

    private static void TrySaveSnapshot(DailyActivitySnapshot snapshot)
    {
        try { SaveSnapshot(snapshot); }
        catch (Exception ex) { AppLog.Write($"Sauvegarde de l’historique d’activité impossible : {ex.Message}"); }
    }

    private static async Task SaveSnapshotAsync(DailyActivitySnapshot snapshot, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(ReportsFolder);
        var path = Path.Combine(ReportsFolder, $"rapport-{snapshot.Date:yyyy-MM-dd}.json");
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(snapshot, JsonOptions), cancellationToken);
        File.Move(temporary, path, overwrite: true);
    }

    private void RestoreTodaySnapshot()
    {
        var snapshot = LoadSnapshots().Where(item => item.Date.Date == DateTime.Today)
            .OrderByDescending(item => item.GeneratedAt).FirstOrDefault();
        if (snapshot is null) return;
        lock (_sync)
        {
            foreach (var item in snapshot.ApplicationSeconds)
                _applicationDurations[item.Key] = TimeSpan.FromSeconds(Math.Max(0, item.Value));
            foreach (var file in snapshot.ModifiedFiles ?? [])
                if (File.Exists(file.FullPath)) _modifiedFiles[file.FullPath] = file.ModifiedAt;
            _trackingDate = DateTime.Today;
        }
    }

    private string? ReadForegroundApplication()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero) return null;
        _ = GetWindowThreadProcessId(window, out var processId);
        if (processId == 0 || processId == Environment.ProcessId) return null;
        if (_processNames.TryGetValue(processId, out var cached)) return cached;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            var description = process.MainModule?.FileVersionInfo.FileDescription?.Trim();
            var name = string.IsNullOrWhiteSpace(description) ? FriendlyProcessName(process.ProcessName) : description;
            _processNames[processId] = name;
            return name;
        }
        catch { return null; }
    }

    private static string FormatDuration(TimeSpan duration) => duration.TotalHours >= 1
        ? $"{(int)duration.TotalHours} h {duration.Minutes:00} min"
        : duration.TotalMinutes < 1 ? $"{Math.Max(0, (int)Math.Round(duration.TotalSeconds))} s"
        : $"{Math.Max(1, (int)Math.Round(duration.TotalMinutes))} min";

    private static string ReportsFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PersonalAppsHub", "ActivityReports");

    private static IReadOnlyList<DailyActivitySnapshot> LoadSnapshots()
    {
        if (!Directory.Exists(ReportsFolder)) return Array.Empty<DailyActivitySnapshot>();
        var results = new List<DailyActivitySnapshot>();
        foreach (var path in Directory.EnumerateFiles(ReportsFolder, "rapport-*.json"))
        {
            try
            {
                var snapshot = JsonSerializer.Deserialize<DailyActivitySnapshot>(File.ReadAllText(path));
                if (snapshot != null) results.Add(snapshot);
            }
            catch { }
        }
        return results;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string FormatChange(double current, double previous)
    {
        if (previous <= 0) return current <= 0 ? "stable" : "nouvelle donnée";
        var percentage = (current - previous) / previous * 100;
        return $"{(percentage >= 0 ? "+" : string.Empty)}{percentage:0}%";
    }

    private static bool IsUsefulPersonalFile(FileInfo file)
    {
        var segments = file.FullName.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                                    segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                                    segment.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                                    segment.Equals(".vs", StringComparison.OrdinalIgnoreCase) ||
                                    segment.Equals("node_modules", StringComparison.OrdinalIgnoreCase))) return false;
        return file.Extension.ToLowerInvariant() is not (".dll" or ".pdb" or ".cache" or ".tmp" or ".log" or ".obj" or ".ilk");
    }

    private static string GetFolderLabel(FileInfo file)
    {
        var mappings = new[]
        {
            (Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Bureau"),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Documents"),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Images"),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Musique"),
            (Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Vidéos"),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), "Téléchargements")
        };
        return mappings.FirstOrDefault(item => file.FullName.StartsWith(item.Item1 + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase)).Item2 ?? "Autres";
    }

    private static string FriendlyProcessName(string name) => name.ToLowerInvariant() switch
    {
        "chrome" => "Google Chrome", "msedge" => "Microsoft Edge", "firefox" => "Mozilla Firefox",
        "code" => "Visual Studio Code", "devenv" => "Visual Studio", "discord" => "Discord",
        "explorer" => "Explorateur Windows", "steam" => "Steam", "spotify" => "Spotify",
        _ => name
    };

    public void Dispose() => StopMonitoring();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}

public sealed record DailyActivitySnapshot(DateTime Date, DateTime GeneratedAt,
    Dictionary<string, double> ApplicationSeconds, int ModifiedFileCount,
    List<ActivityFileSnapshot>? ModifiedFiles = null);
public sealed record ActivityFileSnapshot(string FullPath, DateTime ModifiedAt);
public sealed record ActivityDayStatistic(DateTime Date, double Seconds, int ModifiedFileCount);
public sealed record ActivityApplicationStatistic(string Name, double Seconds);
public sealed record ActivityStatistics(IReadOnlyList<ActivityDayStatistic> Days,
    IReadOnlyList<ActivityApplicationStatistic> Applications, IReadOnlyList<ActivityFileSnapshot> RecentFiles,
    DateTime? OldestRecordedDate);
