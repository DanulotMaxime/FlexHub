using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace PersonalAppsHub.Services;

public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private readonly int _id;
    private HwndSource? _source;
    public event EventHandler? Pressed;
    public HotkeyService(int id = 9471) => _id = id;
    public bool Register(IntPtr handle, string shortcut)
    {
        Unregister(); _source = HwndSource.FromHwnd(handle); _source?.AddHook(Hook);
        var parts = shortcut.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        uint modifiers = 0; Key key = Key.None;
        foreach (var part in parts)
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= 0x0002;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= 0x0001;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= 0x0004;
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase)) modifiers |= 0x0008;
            else Enum.TryParse(part, true, out key);
        }
        return key != Key.None && RegisterHotKey(handle, _id, modifiers | 0x4000, (uint)KeyInterop.VirtualKeyFromKey(key));
    }
    public void Unregister() { if (_source != null) { UnregisterHotKey(_source.Handle, _id); _source.RemoveHook(Hook); _source = null; } }
    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) { if (msg == WmHotkey && wParam.ToInt32() == _id) { handled = true; Pressed?.Invoke(this, EventArgs.Empty); } return IntPtr.Zero; }
    public void Dispose() => Unregister();
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
}
