using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PersonalAppsHub.Services;

public sealed record NvmeSmartData(int TemperatureC, int PercentageUsed, ulong PowerOnHours,
    ulong PowerCycles, ulong UnsafeShutdowns, ulong MediaErrors, ulong DataReadBytes,
    ulong DataWrittenBytes, byte CriticalWarning);

public static class NvmeSmartService
{
    private const uint IoctlStorageQueryProperty = 0x002D1400;
    private const int SmartLogLength = 512;

    public static NvmeSmartData? TryRead(int physicalDriveNumber)
    {
        try
        {
            using var handle = CreateFile($@"\\.\PhysicalDrive{physicalDriveNumber}", 0,
                FileShare.ReadWrite | FileShare.Delete, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);
            if (handle.IsInvalid) return null;

            var buffer = new byte[8 + 40 + SmartLogLength];
            WriteUInt32(buffer, 0, 49); // StorageDeviceProtocolSpecificProperty
            WriteUInt32(buffer, 4, 0);  // PropertyStandardQuery
            WriteUInt32(buffer, 8, 3);  // ProtocolTypeNvme
            WriteUInt32(buffer, 12, 2); // NVMeDataTypeLogPage
            WriteUInt32(buffer, 16, 2); // NVME_LOG_PAGE_HEALTH_INFO
            WriteUInt32(buffer, 24, 40);
            WriteUInt32(buffer, 28, SmartLogLength);

            if (!DeviceIoControl(handle, IoctlStorageQueryProperty, buffer, buffer.Length,
                    buffer, buffer.Length, out var returned, IntPtr.Zero) || returned < 48 + SmartLogLength)
                return null;

            var protocolOffset = (int)ReadUInt32(buffer, 8 + 16);
            var protocolLength = (int)ReadUInt32(buffer, 8 + 20);
            var smartOffset = 8 + protocolOffset;
            if (protocolOffset < 40 || protocolLength < SmartLogLength || smartOffset + SmartLogLength > buffer.Length)
                return null;
            return ParseHealthLog(buffer.AsSpan(smartOffset, SmartLogLength));
        }
        catch { return null; }
    }

    public static NvmeSmartData ParseHealthLog(ReadOnlySpan<byte> data)
    {
        if (data.Length < SmartLogLength) throw new ArgumentException("Page NVMe SMART incomplète.", nameof(data));
        var kelvin = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(1, 2));
        var temperature = kelvin >= 273 ? kelvin - 273 : 0;
        return new NvmeSmartData(
            temperature,
            data[5],
            ReadCounter(data, 128),
            ReadCounter(data, 112),
            ReadCounter(data, 144),
            ReadCounter(data, 160),
            SaturatingMultiply(ReadCounter(data, 32), 512_000),
            SaturatingMultiply(ReadCounter(data, 48), 512_000),
            data[0]);
    }

    private static ulong ReadCounter(ReadOnlySpan<byte> data, int offset) =>
        BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(offset, 8));

    private static ulong SaturatingMultiply(ulong value, ulong multiplier) =>
        value > ulong.MaxValue / multiplier ? ulong.MaxValue : value * multiplier;

    private static void WriteUInt32(Span<byte> buffer, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset, 4), value);
    private static uint ReadUInt32(ReadOnlySpan<byte> buffer, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(offset, 4));

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, FileShare shareMode,
        IntPtr securityAttributes, FileMode creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint controlCode,
        [In, Out] byte[] inputBuffer, int inputBufferSize, [In, Out] byte[] outputBuffer,
        int outputBufferSize, out int bytesReturned, IntPtr overlapped);
}
