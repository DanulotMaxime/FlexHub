using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace PersonalAppsHub;

public enum WheelAction { None, Correct, Translate, Respond }

public partial class ActionWheelWindow : Window
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(25) };
    private readonly System.Windows.Point _origin;
    private readonly TaskCompletionSource<WheelAction> _completion = new();
    private WheelAction _selected;
    private WheelAction _hoveredAction;
    private DateTime _hoverStartedUtc;

    public ActionWheelWindow()
    {
        InitializeComponent();
        GetCursorPos(out var cursor);
        _origin = new System.Windows.Point(cursor.X, cursor.Y);
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | WsExNoActivate | WsExToolWindow);
            var source = HwndSource.FromHwnd(hwnd);
            var position = source?.CompositionTarget?.TransformFromDevice.Transform(new System.Windows.Point(cursor.X, cursor.Y))
                ?? new System.Windows.Point(cursor.X, cursor.Y);
            Left = Math.Max(SystemParameters.VirtualScreenLeft, Math.Min(position.X - Width / 2, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width));
            Top = Math.Max(SystemParameters.VirtualScreenTop, Math.Min(position.Y - Height / 2, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height));
        };
        _timer.Tick += OnTick;
        Closed += (_, _) => { _timer.Stop(); _completion.TrySetResult(_selected); };
    }

    public async Task<WheelAction> ShowAndWaitAsync()
    {
        Show();
        _timer.Start();
        return await _completion.Task;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        GetCursorPos(out var cursor);
        var dx = cursor.X - _origin.X;
        var dy = cursor.Y - _origin.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        _selected = distance < 48 ? WheelAction.None
            : Math.Abs(dx) > Math.Abs(dy) ? (dx < 0 ? WheelAction.Translate : WheelAction.Respond)
            : dy < 0 ? WheelAction.Correct : WheelAction.None;
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
        else if (distance >= 48 && DateTime.UtcNow - _hoverStartedUtc >= TimeSpan.FromMilliseconds(450))
        {
            _timer.Stop();
            Close();
        }
    }

    private void UpdateSelection()
    {
        var normal = new SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 43, 38));
        var selected = new SolidColorBrush(System.Windows.Media.Color.FromRgb(177, 103, 48));
        CorrectionZone.Background = _selected == WheelAction.Correct ? selected : normal;
        TranslationZone.Background = _selected == WheelAction.Translate ? selected : normal;
        ResponseZone.Background = _selected == WheelAction.Respond ? selected : normal;
        CancelZone.Background = _selected == WheelAction.None ? selected : normal;
        CurrentActionText.Text = _selected switch { WheelAction.Correct => "Corriger", WheelAction.Translate => "Traduire", WheelAction.Respond => "Répondre", _ => "Annuler" };
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X; public int Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int virtualKey);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
