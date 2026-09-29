using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;

namespace PersonalAppsHub.Services;

public sealed class LinkHoverSafetyService : IDisposable
{
    private readonly SuspiciousContentAnalysisService _analyzer;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly Window _badge;
    private readonly Border _circle;
    private readonly TextBlock _icon;
    private string? _candidate;
    private DateTime _candidateSince;
    private string? _displayed;

    public LinkHoverSafetyService(SuspiciousContentAnalysisService analyzer)
    {
        _analyzer = analyzer;
        _icon = new TextBlock { FontSize = 20, FontWeight = FontWeights.Bold, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center };
        _circle = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(17), BorderThickness = new Thickness(2), Child = _icon };
        _badge = new Window
        {
            Width = 38, Height = 38, WindowStyle = WindowStyle.None, AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent, ShowInTaskbar = false, Topmost = true,
            ShowActivated = false, IsHitTestVisible = false, Content = _circle, SizeToContent = SizeToContent.Manual
        };
        _timer.Tick += (_, _) => InspectPointer();
    }

    public void Start() { if (!_timer.IsEnabled) _timer.Start(); }
    public void Stop() { _timer.Stop(); _badge.Hide(); _candidate = null; _displayed = null; }

    private void InspectPointer()
    {
        if (!GetCursorPos(out var point)) { Hide(); return; }
        var url = FindLinkAt(point.X, point.Y);
        if (url is null) { Hide(); return; }
        if (!string.Equals(url, _candidate, StringComparison.Ordinal))
        {
            _candidate = url;
            _candidateSince = DateTime.UtcNow;
            _badge.Hide();
            return;
        }
        if (DateTime.UtcNow - _candidateSince < TimeSpan.FromMilliseconds(550)) return;
        if (!string.Equals(url, _displayed, StringComparison.Ordinal))
        {
            try { Apply(_analyzer.AnalyzeUrl(url)); }
            catch { Hide(); return; }
            _displayed = url;
        }
        if (!_badge.IsVisible) _badge.Show();
        var handle = new WindowInteropHelper(_badge).Handle;
        SetWindowPos(handle, HwndTopmost, point.X + 16, point.Y + 20, 0, 0,
            SwpNoSize | SwpNoActivate | SwpShowWindow);
    }

    private void Apply(SuspiciousContentAnalysis result)
    {
        var (symbol, color) = result.RiskScore >= 50 ? ("✕", System.Windows.Media.Color.FromRgb(210, 55, 55))
            : result.RiskScore >= 10 ? ("?", System.Windows.Media.Color.FromRgb(236, 156, 66))
            : ("✓", System.Windows.Media.Color.FromRgb(61, 174, 99));
        _icon.Text = symbol;
        _icon.Foreground = System.Windows.Media.Brushes.White;
        _circle.Background = new SolidColorBrush(color);
        _circle.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 255, 255, 255));
        _circle.ToolTip = $"{result.Verdict} · {result.RiskScore}/100 — analyse locale indicative";
    }

    private void Hide() { _badge.Hide(); _candidate = null; _displayed = null; }

    private static string? FindLinkAt(int x, int y)
    {
        try
        {
            var element = AutomationElement.FromPoint(new System.Windows.Point(x, y));
            for (var depth = 0; element is not null && depth < 5; depth++, element = TreeWalker.ControlViewWalker.GetParent(element))
            {
                if (element.Current.ControlType != ControlType.Hyperlink) continue;
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePattern) && valuePattern is ValuePattern value && IsUrl(value.Current.Value)) return value.Current.Value;
                if (IsUrl(element.Current.HelpText)) return element.Current.HelpText;
                if (IsUrl(element.Current.Name)) return element.Current.Name;
            }
        }
        catch (ElementNotAvailableException) { }
        catch (COMException) { }
        return null;
    }

    private static bool IsUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    public void Dispose() { Stop(); _badge.Close(); }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }
}
