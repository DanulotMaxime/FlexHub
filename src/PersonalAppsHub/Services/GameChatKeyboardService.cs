using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace PersonalAppsHub.Services;

/// <summary>Observes (but never consumes) a configurable key used to open/close in-game chat.</summary>
public sealed class GameChatKeyboardService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const uint WmInputLangChangeRequest = 0x0050;
    private const uint SmtoAbortIfHung = 0x0002;
    private const ushort EnglishPrimaryLanguage = 0x09;
    private const ushort FrenchPrimaryLanguage = 0x0c;

    private readonly KeyboardHookDelegate _callback;
    private readonly object _sync = new();
    private IntPtr _hook;
    private System.Threading.Timer? _foregroundTimer;
    private Shortcut _shortcut = new(0x0D, false, false, false, false);
    private bool _keyDown;
    private ManualOverride? _manualOverride;

    public event EventHandler<bool>? StateChanged;

    public GameChatKeyboardService() => _callback = HookCallback;

    public bool Configure(bool enabled, string shortcut)
    {
        Stop();
        if (!enabled || !TryParseShortcut(shortcut, out _shortcut)) return !enabled;

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        _hook = SetWindowsHookEx(WhKeyboardLl, _callback,
            module == null ? IntPtr.Zero : GetModuleHandle(module.ModuleName), 0);
        if (_hook == IntPtr.Zero) return false;

        _foregroundTimer = new System.Threading.Timer(CheckForeground, null, 250, 250);
        return true;
    }

    public void Stop()
    {
        _foregroundTimer?.Dispose();
        _foregroundTimer = null;
        RestorePreviousLayout();
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        _keyDown = false;
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var message = wParam.ToInt32();
            var data = Marshal.PtrToStructure<KeyboardHookData>(lParam);
            if ((message == WmKeyUp || message == WmSysKeyUp) && data.VirtualKey == _shortcut.VirtualKey)
                _keyDown = false;
            else if ((message == WmKeyDown || message == WmSysKeyDown) &&
                     data.VirtualKey == _shortcut.VirtualKey && !_keyDown && ModifiersMatch())
            {
                _keyDown = true;
                ToggleForForegroundApplication();
            }
        }

        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    private bool ModifiersMatch() =>
        IsPressed(0x11) == _shortcut.Control &&
        IsPressed(0x12) == _shortcut.Alt &&
        IsPressed(0x10) == _shortcut.Shift &&
        (IsPressed(0x5B) || IsPressed(0x5C)) == _shortcut.Windows;

    private static bool IsPressed(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private void ToggleForForegroundApplication()
    {
        lock (_sync)
        {
            if (_manualOverride != null)
            {
                RestorePreviousLayout();
                return;
            }

            var foreground = GetForegroundWindow();
            var threadId = GetWindowThreadProcessId(foreground, out var processId);
            if (foreground == IntPtr.Zero || threadId == 0 || processId == (uint)Environment.ProcessId) return;

            var previousLayout = GetKeyboardLayout(threadId);
            if (!HasPrimaryLanguage(previousLayout, EnglishPrimaryLanguage)) return;
            var frenchLayout = FindInstalledLayout(FrenchPrimaryLanguage);
            if (frenchLayout == IntPtr.Zero || !RequestLayout(foreground, frenchLayout)) return;

            _manualOverride = new ManualOverride(foreground, previousLayout);
            StateChanged?.Invoke(this, true);
        }
    }

    private void CheckForeground(object? state)
    {
        lock (_sync)
        {
            if (_manualOverride is { } active && GetForegroundWindow() != active.ForegroundWindow)
                RestorePreviousLayout();
        }
    }

    private void RestorePreviousLayout()
    {
        var previous = _manualOverride;
        if (previous is null) return;
        _manualOverride = null;

        var foreground = GetForegroundWindow();
        if (foreground != IntPtr.Zero) RequestLayout(foreground, previous.Layout);
        StateChanged?.Invoke(this, false);
    }

    private static bool TryParseShortcut(string value, out Shortcut shortcut)
    {
        var control = false;
        var alt = false;
        var shift = false;
        var windows = false;
        var key = Key.None;
        foreach (var part in value.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) control = true;
            else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) alt = true;
            else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) shift = true;
            else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase)) windows = true;
            else if (!Enum.TryParse(part, true, out key)) { shortcut = default; return false; }
        }

        if (key == Key.None) { shortcut = default; return false; }
        shortcut = new Shortcut((uint)KeyInterop.VirtualKeyFromKey(key), control, alt, shift, windows);
        return true;
    }

    private static IntPtr FindInstalledLayout(ushort primaryLanguage)
    {
        var count = GetKeyboardLayoutList(0, null);
        var layouts = count > 0 ? new IntPtr[count] : [];
        count = GetKeyboardLayoutList(layouts.Length, layouts);
        return layouts.Take(count).FirstOrDefault(layout => HasPrimaryLanguage(layout, primaryLanguage));
    }

    private static bool HasPrimaryLanguage(IntPtr layout, ushort primaryLanguage) =>
        (unchecked((ushort)(layout.ToInt64() & 0xffff)) & 0x03ff) == primaryLanguage;

    private static bool RequestLayout(IntPtr window, IntPtr layout) =>
        SendMessageTimeout(window, WmInputLangChangeRequest, IntPtr.Zero, layout,
            SmtoAbortIfHung, 250, out _) != IntPtr.Zero;

    public void Dispose() => Stop();

    private readonly record struct Shortcut(uint VirtualKey, bool Control, bool Alt, bool Shift, bool Windows);
    private sealed record ManualOverride(IntPtr ForegroundWindow, IntPtr Layout);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardHookData
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    private delegate IntPtr KeyboardHookDelegate(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, KeyboardHookDelegate callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);
    [DllImport("user32.dll")]
    private static extern int GetKeyboardLayoutList(int count, [Out] IntPtr[]? layouts);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam,
        IntPtr lParam, uint flags, uint timeout, out IntPtr result);
}
