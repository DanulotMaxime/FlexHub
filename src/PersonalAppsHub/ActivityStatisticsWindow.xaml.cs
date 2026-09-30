using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using PersonalAppsHub.Services;

namespace PersonalAppsHub;

public partial class ActivityStatisticsWindow : Window
{
    public ActivityStatisticsWindow(DailyActivityReportService service)
    {
        InitializeComponent();
        var statistics = service.GetStatistics();
        var maximumDay = Math.Max(1, statistics.Days.Max(day => day.Seconds));
        DayChart.ItemsSource = statistics.Days.Select(day => new DayView(day.Date.ToString("ddd dd/MM"),
            FormatDuration(day.Seconds), Math.Max(2, day.Seconds / maximumDay * 130),
            $"{FormatDuration(day.Seconds)} · {day.ModifiedFileCount} fichier(s) modifié(s)")).ToArray();

        var maximumApplication = Math.Max(1, statistics.Applications.Select(item => item.Seconds).DefaultIfEmpty().Max());
        ApplicationChart.ItemsSource = statistics.Applications.Select(item => new ApplicationView(item.Name,
            FormatDuration(item.Seconds), Math.Max(2, item.Seconds / maximumApplication * 500))).ToArray();
        RecentFiles.ItemsSource = statistics.RecentFiles.Select(file => new FileView(Path.GetFileName(file.FullPath),
            Path.GetDirectoryName(file.FullPath) ?? "", file.FullPath, file.ModifiedAt.ToString("dd/MM/yyyy HH:mm"),
            IsPreviewableImage(file.FullPath) ? file.FullPath : null)).ToArray();

        var total = statistics.Days.Sum(day => day.Seconds);
        var recordedDays = statistics.Days.Count(day => day.Seconds > 0 || day.ModifiedFileCount > 0);
        var since = statistics.OldestRecordedDate is { } oldest ? $" Historique disponible depuis le {oldest:dd/MM/yyyy}." : "";
        HistorySummary.Text = $"{recordedDays}/7 jour(s) renseigné(s) · {FormatDuration(total)} mesurées · {statistics.RecentFiles.Count} fichier(s) récent(s).{since}";
    }

    private void RecentFiles_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var exists = RecentFiles.SelectedItem is FileView file && File.Exists(file.FullPath);
        OpenFile.IsEnabled = exists;
        OpenLocation.IsEnabled = exists;
        FileStatus.Text = RecentFiles.SelectedItem is FileView selected ? selected.FullPath : "";
        ImagePreview.Source = null;
        ImagePreviewBorder.Visibility = Visibility.Collapsed;
        if (exists && RecentFiles.SelectedItem is FileView { ThumbnailPath: { } previewPath })
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 520;
                image.UriSource = new Uri(previewPath, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                ImagePreview.Source = image;
                ImagePreviewBorder.Visibility = Visibility.Visible;
            }
            catch { ImagePreviewBorder.Visibility = Visibility.Collapsed; }
        }
    }

    private void RecentFiles_OnMouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenSelected(false);
    private void OpenFile_OnClick(object sender, RoutedEventArgs e) => OpenSelected(false);
    private void OpenLocation_OnClick(object sender, RoutedEventArgs e) => OpenSelected(true);
    private void Close_OnClick(object sender, RoutedEventArgs e) => Close();

    private void OpenSelected(bool location)
    {
        if (RecentFiles.SelectedItem is not FileView file || !File.Exists(file.FullPath))
        {
            FileStatus.Text = "Ce fichier n’existe plus à cet emplacement.";
            return;
        }
        try
        {
            if (location) Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file.FullPath}\"") { UseShellExecute = true });
            else Process.Start(new ProcessStartInfo(file.FullPath) { UseShellExecute = true });
        }
        catch (Exception ex) { FileStatus.Text = $"Ouverture impossible : {ex.Message}"; }
    }

    private static bool IsPreviewableImage(string path) => Path.GetExtension(path).ToLowerInvariant() is
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".tif" or ".tiff" or ".webp";

    private static string FormatDuration(double seconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return duration.TotalHours >= 1 ? $"{(int)duration.TotalHours} h {duration.Minutes:00} min"
            : duration.TotalMinutes >= 1 ? $"{Math.Max(1, (int)Math.Round(duration.TotalMinutes))} min"
            : $"{Math.Max(0, (int)Math.Round(duration.TotalSeconds))} s";
    }

    private sealed record DayView(string DayText, string DurationText, double BarHeight, string Tooltip);
    private sealed record ApplicationView(string Name, string DurationText, double BarWidth);
    private sealed record FileView(string Name, string Folder, string FullPath, string ModifiedText, string? ThumbnailPath);
}
