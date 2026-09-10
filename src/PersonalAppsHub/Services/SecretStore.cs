using System.Runtime.InteropServices;
using System.Text;
using System.IO;

namespace PersonalAppsHub.Services;

public static class SecretStore
{
    private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PersonalAppsHub");
    private static string FilePath(string provider) => Path.Combine(Folder, $"{provider.ToLowerInvariant()}.key");

    public static bool HasKey(string provider) => File.Exists(FilePath(provider));
    public static void Save(string provider, string value) { Directory.CreateDirectory(Folder); File.WriteAllBytes(FilePath(provider), Protect(Encoding.UTF8.GetBytes(value))); }
    public static string? Load(string provider) { try { var path = FilePath(provider); return File.Exists(path) ? Encoding.UTF8.GetString(Unprotect(File.ReadAllBytes(path))) : null; } catch { return null; } }
    public static void Delete(string provider) { var path = FilePath(provider); if (File.Exists(path)) File.Delete(path); }

    private static byte[] Protect(byte[] data) => Crypt(data, true);
    private static byte[] Unprotect(byte[] data) => Crypt(data, false);
    private static byte[] Crypt(byte[] data, bool protect)
    {
        var input = new DataBlob(); var output = new DataBlob();
        try
        {
            input.Size = data.Length; input.Data = Marshal.AllocHGlobal(data.Length); Marshal.Copy(data, 0, input.Data, data.Length);
            var ok = protect ? CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, ref output)
                             : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, ref output);
            if (!ok) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            var result = new byte[output.Size]; Marshal.Copy(output.Data, result, 0, output.Size); return result;
        }
        finally { if (input.Data != IntPtr.Zero) Marshal.FreeHGlobal(input.Data); if (output.Data != IntPtr.Zero) LocalFree(output.Data); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct DataBlob { public int Size; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true)] private static extern bool CryptProtectData(ref DataBlob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, ref DataBlob output);
    [DllImport("crypt32.dll", SetLastError = true)] private static extern bool CryptUnprotectData(ref DataBlob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, ref DataBlob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
}
