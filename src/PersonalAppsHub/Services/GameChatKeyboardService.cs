using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace PersonalAppsHub.Services;

/// <summary>
/// Memorizes the first QWERTY window in which the chat shortcut is pressed as the game.
/// The game keeps QWERTY outside chat; every other window uses AZERTY.
/// </summary>
public sealed class GameChatKeyboardService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int WmLButtonDown = 0x0201;
    private const int WmRButtonDown = 0x0204;
    private const int WmMButtonDown = 0x0207;
    private const int WmXButtonDown = 0x020B;
    private const uint WmInputLangChangeRequest = 0x0050;
    private const uint SmtoAbortIfHung = 0x0002;
    private const ushort EnglishPrimaryLanguage = 0x09;
    private const ushort FrenchPrimaryLanguage = 0x0c;
    private const uint VkEscape = 0x1B;

    private readonly KeyboardHookDelegate _keyboardCallback;
    private readonly MouseHookDelegate _mouseCallback;
    private readonly object _sync = new();
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private System.Threading.Timer? _foregroundTimer;
    private Shortcut _shortcut = new(0x0D, false, false, false, false);
    private bool _keyDown;
    private bool _shortcutPressAccepted;
    private bool _escapeDown;
    private bool _chatActive;
    private RegisteredGame? _registeredGame;
    private IntPtr _lastForeground;

    public event EventHandler<bool>? StateChanged;

    public GameChatKeyboardService()
    {
        _keyboardCallback = KeyboardHookCallback;
        _mouseCallback = MouseHookCallback;
    }

    public bool Configure(bool enabled, string shortcut)
    {
        Stop();
        if (!enabled || !TryParseShortcut(shortcut, out _shortcut)) return !enabled;

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        var moduleHandle = module == null ? IntPtr.Zero : GetModuleHandle(module.ModuleName);
        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardCallback, moduleHandle, 0);
        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseCallback, moduleHandle, 0);
        if (_keyboardHook == IntPtr.Zero || _mouseHook == IntPtr.Zero)
        {
            Stop();
            return false;
        }

        _foregroundTimer = new System.Threading.Timer(CheckForeground, null, 100, 100);
        return true;
    }

    public void Stop()
    {
        _foregroundTimer?.Dispose();
        _foregroundTimer = null;
        lock (_sync)
        {
            EndChat(restoreGameLayout: true);
            _registeredGame = null;
            _lastForeground = IntPtr.Zero;
        }
        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }
        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
        _keyDown = false;
        _escapeDown = false;
    }

    private IntPtr KeyboardHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var message = wParam.ToInt32();
            var data = Marshal.PtrToStructure<KeyboardHookData>(lParam);
            var isKeyDown = message is WmKeyDown or WmSysKeyDown;
            var isKeyUp = message is WmKeyUp or WmSysKeyUp;

            lock (_sync)
            {
                if (data.VirtualKey == VkEscape)
                {
                    if (isKeyUp)
                    {
                        var mustEndChat = _escapeDown && _chatActive;
                        _escapeDown = false;
                        if (mustEndChat) QueueAfterInput(() => EndChat(restoreGameLayout: true));
                    }
                    else if (isKeyDown && !_escapeDown)
                    {
                        _escapeDown = true;
                    }
                }

                if (data.VirtualKey == _shortcut.VirtualKey)
                {
                    if (isKeyUp)
                    {
                        var mustToggle = _keyDown && _shortcutPressAccepted;
                        _keyDown = false;
                        _shortcutPressAccepted = false;
                        if (mustToggle) QueueAfterInput(ToggleChatForForegroundWindow);
                    }
                    else if (isKeyDown && !_keyDown)
                    {
                        _keyDown = true;
                        _shortcutPressAccepted = ModifiersMatch();
                    }
                }
            }
        }

        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam.ToInt32() is WmLButtonDown or WmRButtonDown or WmMButtonDown or WmXButtonDown)
        {
            lock (_sync)
            {
                if (_chatActive) EndChat(restoreGameLayout: true);
            }
        }
        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private void QueueAfterInput(Action action)
    {
        _ = Task.Delay(30).ContinueWith(_ =>
        {
            lock (_sync)
            {
                if (_keyboardHook != IntPtr.Zero) action();
            }
        }, TaskScheduler.Default);
    }

    private bool ModifiersMatch() =>
        IsPressed(0x11) == _shortcut.Control &&
        IsPressed(0x12) == _shortcut.Alt &&
        IsPressed(0x10) == _shortcut.Shift &&
        (IsPressed(0x5B) || IsPressed(0x5C)) == _shortcut.Windows;

    private static bool IsPressed(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private void ToggleChatForForegroundWindow()
    {
        var foreground = GetForegroundWindow();
        var threadId = GetWindowThreadProcessId(foreground, out var processId);
        if (foreground == IntPtr.Zero || threadId == 0 || processId == (uint)Environment.ProcessId) return;

        if (_registeredGame is { } game)
        {
            if (foreground != game.Window) return;
            if (_chatActive)
            {
                EndChat(restoreGameLayout: true);
                return;
            }

            StartChat(game);
            return;
        }

        var currentLayout = GetKeyboardLayout(threadId);
        if (!HasPrimaryLanguage(currentLayout, EnglishPrimaryLanguage)) return;

        var registered = new RegisteredGame(foreground, processId, currentLayout);
        _registeredGame = registered;
        _lastForeground = foreground;
        AppLog.Write($"Clavier de jeu : fenêtre QWERTY mémorisée (PID {processId}).");
        StartChat(registered);
    }

    private void StartChat(RegisteredGame game)
    {
        var frenchLayout = FindInstalledLayout(FrenchPrimaryLanguage);
        if (frenchLayout == IntPtr.Zero || !RequestLayout(game.Window, frenchLayout)) return;

        _chatActive = true;
        StateChanged?.Invoke(this, true);
    }

    private void EndChat(bool restoreGameLayout)
    {
        if (!_chatActive) return;
        _chatActive = false;
        if (restoreGameLayout && _registeredGame is { } game && IsWindow(game.Window))
            RequestLayout(game.Window, game.Layout);
        StateChanged?.Invoke(this, false);
    }

    private void CheckForeground(object? state)
    {
        lock (_sync)
        {
            var foreground = GetForegroundWindow();
            if (foreground == _lastForeground) return;
            _lastForeground = foreground;

            if (_registeredGame is not { } game) return;
            if (!IsWindow(game.Window))
            {
                EndChat(restoreGameLayout: false);
                _registeredGame = null;
                EnsureFrenchLayout(foreground);
                AppLog.Write("Clavier de jeu : fenêtre mémorisée fermée.");
                return;
            }

            if (foreground == game.Window)
            {
                EndChat(restoreGameLayout: false);
                RequestLayout(game.Window, game.Layout);
            }
            else
            {
                EndChat(restoreGameLayout: false);
                EnsureFrenchLayout(foreground);
            }
        }
    }

    private static void EnsureFrenchLayout(IntPtr window)
    {
        if (window == IntPtr.Zero) return;
        var frenchLayout = FindInstalledLayout(FrenchPrimaryLanguage);
        if (frenchLayout != IntPtr.Zero) RequestLayout(window, frenchLayout);
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
    private sealed record RegisteredGame(IntPtr Window, uint ProcessId, IntPtr Layout);

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
    private delegate IntPtr MouseHookDelegate(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, KeyboardHookDelegate callback, IntPtr module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, MouseHookDelegate callback, IntPtr module, uint threadId);
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
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam,
        IntPtr lParam, uint flags, uint timeout, out IntPtr result);
}
