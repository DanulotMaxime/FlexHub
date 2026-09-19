using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace PersonalAppsHub.Services;

/// <summary>
/// Uses the French keyboard layout while an editable control has focus, then restores
/// the layout that was active before the user entered the text field.
/// </summary>
public sealed class KeyboardLayoutMonitorService : IDisposable
{
    private const uint EventSystemForeground = 0x0003;
    private const uint EventObjectFocus = 0x8005;
    private const uint WineventOutOfContext = 0x0000;
    private const uint WmInputLangChangeRequest = 0x0050;
    private const uint SmtoAbortIfHung = 0x0002;
    private const ushort EnglishPrimaryLanguage = 0x09;
    private const ushort FrenchPrimaryLanguage = 0x0c;

    private readonly WinEventDelegate _callback;
    private readonly object _sync = new();
    private IntPtr _hook;
    private IntPtr _focusHook;
    private ActiveOverride? _activeOverride;
    private System.Threading.Timer? _foregroundTimer;

    public KeyboardLayoutMonitorService() => _callback = OnForegroundChanged;

    public bool IsRunning => _hook != IntPtr.Zero;

    public void Start()
    {
        if (IsRunning) return;

        _hook = SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero,
            _callback, 0, 0, WineventOutOfContext);
        _focusHook = SetWinEventHook(EventObjectFocus, EventObjectFocus, IntPtr.Zero,
            _callback, 0, 0, WineventOutOfContext);
        if (_hook == IntPtr.Zero || _focusHook == IntPtr.Zero)
        {
            AppLog.Write("Surveillance du clavier : installation du hook impossible.");
            Stop();
            return;
        }

        _foregroundTimer = new System.Threading.Timer(CheckForegroundWindow, null, 150, 150);
        HandleForegroundWindow(GetForegroundWindow());
    }

    public void Stop()
    {
        _foregroundTimer?.Dispose();
        _foregroundTimer = null;
        if (_hook != IntPtr.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
        if (_focusHook != IntPtr.Zero)
        {
            UnhookWinEvent(_focusHook);
            _focusHook = IntPtr.Zero;
        }

        RestorePreviousLayout(GetForegroundWindow());
    }

    private void OnForegroundChanged(IntPtr hook, uint eventType, IntPtr window, int objectId,
        int childId, uint eventThread, uint eventTime) => HandleForegroundWindow(GetForegroundWindow());

    private void CheckForegroundWindow(object? state)
    {
        lock (_sync)
        {
            // Focus/foreground hooks detect entry into fields. Polling is only a safety net
            // while an override is active, avoiding continuous UI Automation allocations.
            if (_activeOverride is null) return;
        }
        HandleForegroundWindow(GetForegroundWindow());
    }

    private void HandleForegroundWindow(IntPtr window)
    {
        lock (_sync)
        {
            if (window == IntPtr.Zero) return;

            var threadId = GetWindowThreadProcessId(window, out _);
            if (threadId == 0) return;
            var focusedWindow = GetFocusedWindow(threadId);
            if (_activeOverride is { } active &&
                active.ForegroundWindow == window && active.InputWindow == focusedWindow)
                return;

            RestorePreviousLayout(window);
            if (focusedWindow == IntPtr.Zero || !IsEditableControl(focusedWindow)) return;

            var currentLayout = GetKeyboardLayout(threadId);
            if (!HasPrimaryLanguage(currentLayout, EnglishPrimaryLanguage)) return;

            var frenchLayout = FindInstalledLayout(FrenchPrimaryLanguage);
            if (frenchLayout == IntPtr.Zero)
            {
                AppLog.Write("Surveillance du clavier : aucune disposition française installée.");
                return;
            }

            if (!RequestLayout(focusedWindow, frenchLayout) && !RequestLayout(window, frenchLayout)) return;

            _activeOverride = new ActiveOverride(window, focusedWindow, threadId, currentLayout);
            AppLog.Write("Disposition française activée temporairement dans une zone de saisie.");
        }
    }

    private void RestorePreviousLayout(IntPtr foregroundWindow)
    {
        var previous = _activeOverride;
        if (previous is null) return;

        _activeOverride = null;
        var foregroundThread = foregroundWindow == IntPtr.Zero
            ? 0
            : GetWindowThreadProcessId(foregroundWindow, out _);
        var fallbackTarget = foregroundWindow != IntPtr.Zero && IsWindow(foregroundWindow)
            ? foregroundWindow
            : IsWindow(previous.ForegroundWindow) ? previous.ForegroundWindow : IntPtr.Zero;
        var restoreTarget = foregroundThread == 0 ? IntPtr.Zero : GetFocusedWindow(foregroundThread);
        if (restoreTarget == IntPtr.Zero) restoreTarget = fallbackTarget;

        var restored = restoreTarget != IntPtr.Zero && RequestLayout(restoreTarget, previous.Layout);
        if (!restored && fallbackTarget != IntPtr.Zero && fallbackTarget != restoreTarget)
            restored = RequestLayout(fallbackTarget, previous.Layout);
        if (restored)
            AppLog.Write("Disposition du clavier précédente restaurée.");
    }

    private static IntPtr GetFocusedWindow(uint threadId)
    {
        var info = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
        return GetGUIThreadInfo(threadId, ref info) ? info.Focus : IntPtr.Zero;
    }

    private static bool IsEditableControl(IntPtr window)
    {
        var threadId = GetWindowThreadProcessId(window, out _);
        var guiInfo = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
        if (threadId != 0 && GetGUIThreadInfo(threadId, ref guiInfo) && guiInfo.Caret != IntPtr.Zero)
            return true;

        try
        {
            var element = AutomationElement.FocusedElement;
            if (element != null && element.Current.IsEnabled && element.Current.IsKeyboardFocusable)
            {
                var controlType = element.Current.ControlType;
                if (controlType == ControlType.Edit || controlType == ControlType.Document) return true;
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern) &&
                    pattern is ValuePattern valuePattern && !valuePattern.Current.IsReadOnly)
                    return true;
            }
        }
        catch (ElementNotAvailableException) { }
        catch (InvalidOperationException) { }
        catch (COMException) { }

        var className = new StringBuilder(128);
        if (GetClassName(window, className, className.Capacity) <= 0) return false;
        var name = className.ToString();
        return name.StartsWith("Edit", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("RichEdit", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Scintilla", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("TextBox", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("RenderWidgetHost", StringComparison.OrdinalIgnoreCase);
    }

    private static IntPtr FindInstalledLayout(ushort primaryLanguage)
    {
        var count = GetKeyboardLayoutList(0, null);
        if (count <= 0) return IntPtr.Zero;

        var layouts = new IntPtr[count];
        count = GetKeyboardLayoutList(layouts.Length, layouts);
        for (var index = 0; index < count; index++)
        {
            if (HasPrimaryLanguage(layouts[index], primaryLanguage)) return layouts[index];
        }

        return IntPtr.Zero;
    }

    private static bool HasPrimaryLanguage(IntPtr layout, ushort primaryLanguage)
    {
        var languageId = unchecked((ushort)(layout.ToInt64() & 0xffff));
        return (languageId & 0x03ff) == primaryLanguage;
    }

    private static bool RequestLayout(IntPtr window, IntPtr layout) =>
        SendMessageTimeout(window, WmInputLangChangeRequest, IntPtr.Zero, layout,
            SmtoAbortIfHung, 250, out _) != IntPtr.Zero;

    public void Dispose() => Stop();

    private sealed record ActiveOverride(IntPtr ForegroundWindow, IntPtr InputWindow, uint ThreadId, IntPtr Layout);

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size;
        public uint Flags;
        public IntPtr Active;
        public IntPtr Focus;
        public IntPtr Capture;
        public IntPtr MenuOwner;
        public IntPtr MoveSize;
        public IntPtr Caret;
        public NativeRect CaretRect;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate void WinEventDelegate(IntPtr hook, uint eventType, IntPtr window, int objectId,
        int childId, uint eventThread, uint eventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module,
        WinEventDelegate callback, uint processId, uint threadId, uint flags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    private static extern int GetKeyboardLayoutList(int count, [Out] IntPtr[]? layouts);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder className, int maxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam,
        IntPtr lParam, uint flags, uint timeout, out IntPtr result);
}
