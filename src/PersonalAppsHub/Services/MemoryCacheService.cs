using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PersonalAppsHub.Services;

public sealed record MemoryCacheSnapshot(ulong AvailableBytes, ulong TotalBytes, uint MemoryLoadPercent)
{
    public string AvailableText => TemporaryFileCleanupService.FormatSize((long)Math.Min(AvailableBytes, long.MaxValue));
}

public sealed class MemoryCacheService
{
    private const string ElevatedArgument = "--purge-standby-memory";
    private const int SystemMemoryListInformation = 80;
    private const int MemoryPurgeStandbyList = 4;
    private const uint TokenAdjustPrivileges = 0x20;
    private const uint TokenQuery = 0x8;
    private const uint SePrivilegeEnabled = 0x2;

    public static bool IsElevatedPurgeRequest(IEnumerable<string> arguments) =>
        arguments.Any(argument => string.Equals(argument, ElevatedArgument, StringComparison.OrdinalIgnoreCase));

    public MemoryCacheSnapshot ReadSnapshot()
    {
        var status = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(status)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new MemoryCacheSnapshot(status.AvailablePhysical, status.TotalPhysical, status.MemoryLoad);
    }

    public async Task PurgeStandbyListElevatedAsync()
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Chemin de FlexHub introuvable.");
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            Arguments = ElevatedArgument,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        }) ?? throw new InvalidOperationException("Le processus administrateur n’a pas pu démarrer.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Windows n’a pas pu purger la mémoire en attente (code {process.ExitCode}).");
    }

    public static void PurgeStandbyList()
    {
        EnableProfilePrivilege();
        var command = MemoryPurgeStandbyList;
        var status = NtSetSystemInformation(SystemMemoryListInformation, ref command, sizeof(int));
        if (status != 0) throw new InvalidOperationException($"La purge Windows a échoué (NTSTATUS 0x{status:X8}).");
    }

    private static void EnableProfilePrivilege()
    {
        if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TokenAdjustPrivileges | TokenQuery, out var token))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            if (!LookupPrivilegeValue(null, "SeProfileSingleProcessPrivilege", out var luid))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var privileges = new TokenPrivileges
            {
                PrivilegeCount = 1,
                Privileges = new LuidAndAttributes { Luid = luid, Attributes = SePrivilegeEnabled }
            };
            if (!AdjustTokenPrivileges(token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var error = Marshal.GetLastWin32Error();
            if (error != 0) throw new Win32Exception(error);
        }
        finally { CloseHandle(token); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private sealed class MemoryStatusEx
    {
        public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Luid { public uint LowPart; public int HighPart; }
    [StructLayout(LayoutKind.Sequential)] private struct LuidAndAttributes { public Luid Luid; public uint Attributes; }
    [StructLayout(LayoutKind.Sequential)] private struct TokenPrivileges { public uint PrivilegeCount; public LuidAndAttributes Privileges; }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx buffer);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool LookupPrivilegeValue(string? systemName, string name, out Luid luid);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TokenPrivileges newState, uint length, IntPtr previousState, IntPtr returnLength);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("ntdll.dll")] private static extern int NtSetSystemInformation(int informationClass, ref int information, int informationLength);
}
