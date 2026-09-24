using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace PersonalAppsHub;

public enum WheelAction
{
    None,
    Cancel,
    Correct,
    Reformulate,
    Simplify,
    TranslateForward,
    TranslateReverse,
    Respond,
    SummarizeConversation,
    DefineWord
}

public partial class ActionWheelWindow : Window
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const double AnchorX = 480;
    private const double AnchorY = 380;
    private const double InitialDeadZoneRadius = 45;
    private static readonly TimeSpan SelectionDelay = TimeSpan.FromMilliseconds(450);

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(25) };
    private readonly System.Windows.Point _origin;
    private readonly TaskCompletionSource<WheelAction> _completion = new();
    private readonly List<(FrameworkElement Element, WheelAction Action)> _zones;
    private readonly SolidColorBrush _normalBrush = new(System.Windows.Media.Color.FromRgb(42, 39, 37));
    private readonly SolidColorBrush _selectedBrush = new(System.Windows.Media.Color.FromRgb(177, 103, 48));
    private readonly SolidColorBrush _cancelBrush = new(System.Windows.Media.Color.FromRgb(23, 22, 22));
    private readonly SolidColorBrush _cancelSelectedBrush = new(System.Windows.Media.Color.FromRgb(84, 40, 43));

    private WheelAction _selected;
    private WheelAction _hoveredAction;
    private DateTime _hoverStartedUtc;

    public ActionWheelWindow(
        string sourceLanguage,
        string targetLanguage,
        bool correctorEnabled,
        bool translatorEnabled,
        bool responseGeneratorEnabled,
        bool reformulatorEnabled,
        bool simplifierEnabled,
        bool conversationSummarizerEnabled,
        bool wordDefinitionEnabled)
    {
        InitializeComponent();
        TranslationForwardText.Text = $"{sourceLanguage} > {targetLanguage}";
        TranslationReverseText.Text = $"{targetLanguage} > {sourceLanguage}";

        CorrectionZone.Visibility = correctorEnabled ? Visibility.Visible : Visibility.Collapsed;
        TranslationForwardZone.Visibility = translatorEnabled ? Visibility.Visible : Visibility.Collapsed;
        TranslationReverseZone.Visibility = translatorEnabled ? Visibility.Visible : Visibility.Collapsed;
        CustomToneZone.Visibility = responseGeneratorEnabled ? Visibility.Visible : Visibility.Collapsed;
        ReformulateZone.Visibility = responseGeneratorEnabled && reformulatorEnabled ? Visibility.Visible : Visibility.Collapsed;
        SimplifyZone.Visibility = responseGeneratorEnabled && simplifierEnabled ? Visibility.Visible : Visibility.Collapsed;
        SummarizeZone.Visibility = responseGeneratorEnabled && conversationSummarizerEnabled ? Visibility.Visible : Visibility.Collapsed;
        DefineWordZone.Visibility = responseGeneratorEnabled && wordDefinitionEnabled ? Visibility.Visible : Visibility.Collapsed;

        _zones =
        [
            (CorrectionZone, WheelAction.Correct),
            (ReformulateZone, WheelAction.Reformulate),
            (SimplifyZone, WheelAction.Simplify),
            (TranslationForwardZone, WheelAction.TranslateForward),
            (TranslationReverseZone, WheelAction.TranslateReverse),
            (CustomToneZone, WheelAction.Respond),
            (SummarizeZone, WheelAction.SummarizeConversation),
            (DefineWordZone, WheelAction.DefineWord),
            (CancelZone, WheelAction.Cancel)
        ];
        ArrangeVisibleActions();

        GetCursorPos(out var cursor);
        _origin = new System.Windows.Point(cursor.X, cursor.Y);
        SourceInitialized += (_, _) => InitializeNativeWindow(cursor);
        _timer.Tick += OnTick;
        Closed += (_, _) =>
        {
            _timer.Stop();
            _completion.TrySetResult(_selected is WheelAction.Cancel ? WheelAction.None : _selected);
        };
    }

    private void ArrangeVisibleActions()
    {
        var actionElements = _zones
            .Where(zone => zone.Action != WheelAction.Cancel && zone.Element.Visibility == Visibility.Visible)
            .Select(zone => zone.Element)
            .ToArray();
        for (var index = 0; index < actionElements.Length; index++)
        {
            var angle = -Math.PI / 2 + index * 2 * Math.PI / actionElements.Length;
            var centerX = AnchorX + 285 * Math.Cos(angle);
            var centerY = AnchorY + 235 * Math.Sin(angle);
            System.Windows.Controls.Canvas.SetLeft(actionElements[index], centerX - actionElements[index].Width / 2);
            System.Windows.Controls.Canvas.SetTop(actionElements[index], centerY - actionElements[index].Height / 2);
        }
    }

    public async Task<WheelAction> ShowAndWaitAsync()
    {
        Show();
        _timer.Start();
        return await _completion.Task;
    }

    private void InitializeNativeWindow(NativePoint cursor)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | WsExNoActivate | WsExToolWindow);
        var source = HwndSource.FromHwnd(hwnd);
        var position = source?.CompositionTarget?.TransformFromDevice.Transform(new System.Windows.Point(cursor.X, cursor.Y))
            ?? new System.Windows.Point(cursor.X, cursor.Y);

        Left = Math.Max(SystemParameters.VirtualScreenLeft,
            Math.Min(position.X - AnchorX, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width));
        Top = Math.Max(SystemParameters.VirtualScreenTop,
            Math.Min(position.Y - AnchorY, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height));
    }

    private void OnTick(object? sender, EventArgs e)
    {
        GetCursorPos(out var cursor);
        var dx = cursor.X - _origin.X;
        var dy = cursor.Y - _origin.Y;
        var distanceFromOrigin = Math.Sqrt(dx * dx + dy * dy);
        var pointer = PointFromScreen(new System.Windows.Point(cursor.X, cursor.Y));

        _selected = distanceFromOrigin < InitialDeadZoneRadius
            ? WheelAction.None
            : ActionAt(pointer);
        UpdateSelection();

        if ((GetAsyncKeyState(0x1B) & 0x8000) != 0)
        {
            _selected = WheelAction.None;
            _timer.Stop();
            Close();
            return;
        }

        if (_selected != _hoveredAction)
        {
            _hoveredAction = _selected;
            _hoverStartedUtc = DateTime.UtcNow;
        }
        else if (_selected != WheelAction.None && DateTime.UtcNow - _hoverStartedUtc >= SelectionDelay)
        {
            _timer.Stop();
            Close();
        }
    }

    private WheelAction ActionAt(System.Windows.Point pointer)
    {
        foreach (var (element, action) in _zones)
        {
            if (!element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0) continue;
            var bounds = element.TransformToAncestor(this)
                .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            if (bounds.Contains(pointer)) return action;
        }
        return WheelAction.None;
    }

    private void UpdateSelection()
    {
        foreach (var (element, action) in _zones)
        {
            if (element is not System.Windows.Controls.Border border) continue;
            border.Background = action switch
            {
                WheelAction.Cancel when _selected == WheelAction.Cancel => _cancelSelectedBrush,
                WheelAction.Cancel => _cancelBrush,
                _ when _selected == action => _selectedBrush,
                _ => _normalBrush
            };
        }

        CurrentActionText.Text = _selected switch
        {
            WheelAction.Correct => "Corriger",
            WheelAction.Reformulate => "Reformuler",
            WheelAction.Simplify => "Simplifier",
            WheelAction.TranslateForward => "Traduire",
            WheelAction.TranslateReverse => "Traduire en sens inverse",
            WheelAction.Respond => "Ton personnalisé",
            WheelAction.SummarizeConversation => "Résumer la conversation",
            WheelAction.DefineWord => "Définir le mot",
            WheelAction.Cancel => "Relâchez ici pour annuler",
            _ => "Déplacez le pointeur"
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int virtualKey);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
