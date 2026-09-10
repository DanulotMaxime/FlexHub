using System.Runtime.InteropServices;
using Forms = System.Windows.Forms;

namespace PersonalAppsHub.Services;

public static class ClipboardService
{
    private const byte VkControl = 0x11;
    private const byte VkC = 0x43;
    private const byte VkV = 0x56;
    private const uint KeyUp = 0x0002;

    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")] private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int virtualKey);

    public static async Task<(string Text, IntPtr Window, System.Windows.IDataObject? Previous)> CopySelectionAsync()
    {
        var window = GetForegroundWindow();
        System.Windows.IDataObject? previous = null;
        try { previous = System.Windows.Clipboard.GetDataObject(); } catch { }
        await WaitForModifiersReleasedAsync();
        try { System.Windows.Clipboard.Clear(); } catch { }
        SendCtrlKey(VkC);
        var text = "";
        for (var attempt = 0; attempt < 12 && string.IsNullOrEmpty(text); attempt++)
        {
            await Task.Delay(100);
            try { if (System.Windows.Clipboard.ContainsText()) text = System.Windows.Clipboard.GetText(); } catch { }
        }
        return (text, window, previous);
    }

    public static async Task ReplaceAsync(string text, IntPtr target, System.Windows.IDataObject? previous)
    {
        System.Windows.Clipboard.SetText(text); SetForegroundWindow(target); await Task.Delay(180); SendCtrlKey(VkV); await Task.Delay(300);
        Restore(previous);
    }

    public static void Restore(System.Windows.IDataObject? previous)
    {
        try { if (previous != null) System.Windows.Clipboard.SetDataObject(previous, true); } catch { }
    }

    private static void SendCtrlKey(byte key)
    {
        keybd_event(VkControl, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, 0, UIntPtr.Zero);
        keybd_event(key, 0, KeyUp, UIntPtr.Zero);
        keybd_event(VkControl, 0, KeyUp, UIntPtr.Zero);
    }

    private static async Task WaitForModifiersReleasedAsync()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var ctrl = (GetAsyncKeyState(0x11) & 0x8000) != 0;
            var alt = (GetAsyncKeyState(0x12) & 0x8000) != 0;
            var shift = (GetAsyncKeyState(0x10) & 0x8000) != 0;
            var win = (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0;
            if (!ctrl && !alt && !shift && !win) return;
            await Task.Delay(50);
        }
    }
}
