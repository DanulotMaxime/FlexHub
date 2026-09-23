using System.IO;
using System.Management;

namespace PersonalAppsHub.Services;

public sealed record VolumeHealthInfo(string Name, string Label, string Format, long TotalBytes, long FreeBytes)
{
    public double UsedPercent => TotalBytes <= 0 ? 0 : Math.Clamp((TotalBytes - FreeBytes) * 100d / TotalBytes, 0, 100);
    public string SpaceText => $"{TemporaryFileCleanupService.FormatSize(FreeBytes)} libres sur {TemporaryFileCleanupService.FormatSize(TotalBytes)}";
    public string UsageText => $"{UsedPercent:0}% utilisé";
}

public sealed record PhysicalDiskHealthInfo(string Name, string MediaType, string Health, string SizeText,
    string HealthPercentText, string PowerOnHoursText, string TemperatureText,
    string SmartSourceText, string SmartDetailsText);

public sealed record StorageHealthSnapshot(IReadOnlyList<VolumeHealthInfo> Volumes, IReadOnlyList<PhysicalDiskHealthInfo> PhysicalDisks);

public sealed class StorageHealthService
{
    public Task<StorageHealthSnapshot> ReadAsync() => Task.Run(() =>
        new StorageHealthSnapshot(ReadVolumes(), ReadPhysicalDisks()));

    private static IReadOnlyList<VolumeHealthInfo> ReadVolumes()
    {
        var result = new List<VolumeHealthInfo>();
        foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.IsReady && drive.DriveType == DriveType.Fixed))
        {
            try
            {
                result.Add(new VolumeHealthInfo(drive.Name, drive.VolumeLabel, drive.DriveFormat,
                    drive.TotalSize, drive.AvailableFreeSpace));
            }
            catch { }
        }
        return result;
    }

    private static IReadOnlyList<PhysicalDiskHealthInfo> ReadPhysicalDisks()
    {
        var result = new List<PhysicalDiskHealthInfo>();
        try
        {
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            scope.Connect();
            var reliability = ReadReliabilityCounters(scope);
            using var searcher = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT DeviceId, FriendlyName, MediaType, HealthStatus, Size FROM MSFT_PhysicalDisk"));
            foreach (ManagementObject disk in searcher.Get())
            {
                var deviceId = disk["DeviceId"]?.ToString() ?? "";
                var name = disk["FriendlyName"]?.ToString() ?? "Disque inconnu";
                var media = MediaTypeName(Convert.ToUInt16(disk["MediaType"] ?? 0));
                var health = HealthStatusName(Convert.ToUInt16(disk["HealthStatus"] ?? 5));
                var size = Convert.ToInt64(disk["Size"] ?? 0L);
                reliability.TryGetValue(deviceId, out var counters);
                var nvme = int.TryParse(deviceId, out var driveNumber) ? NvmeSmartService.TryRead(driveNumber) : null;
                var wearValue = nvme?.PercentageUsed ?? counters?.Wear;
                var healthPercent = wearValue is int wear
                    ? $"Santé estimée : {RemainingHealthPercent(wear)} %"
                    : "Santé estimée : non communiquée";
                var hoursValue = nvme is not null ? ToDisplayInt(nvme.PowerOnHours) : counters?.PowerOnHours;
                var hours = hoursValue is int powerOnHours
                    ? $"{powerOnHours:N0} h de fonctionnement"
                    : "Heures de fonctionnement non communiquées";
                var temperatureValue = nvme?.TemperatureC ?? counters?.Temperature;
                var temperature = temperatureValue is int temperatureC
                    ? $"{temperatureC} °C"
                    : "Température non communiquée";
                var smartSource = nvme is not null ? "S.M.A.R.T. NVMe natif" : counters is not null ? "Compteurs Windows" : "S.M.A.R.T. indisponible";
                var smartDetails = nvme is not null
                    ? $"Lus {FormatUlongSize(nvme.DataReadBytes)} · Écrits {FormatUlongSize(nvme.DataWrittenBytes)} · " +
                      $"{nvme.PowerCycles:N0} démarrages · {nvme.UnsafeShutdowns:N0} arrêts non sécurisés · {nvme.MediaErrors:N0} erreurs média" +
                      (nvme.CriticalWarning != 0 ? $" · Avertissement critique 0x{nvme.CriticalWarning:X2}" : "")
                    : "Aucun détail S.M.A.R.T. direct communiqué par ce contrôleur.";
                result.Add(new PhysicalDiskHealthInfo(name, media, health,
                    TemporaryFileCleanupService.FormatSize(size), healthPercent, hours, temperature,
                    smartSource, smartDetails));
            }
        }
        catch (Exception ex) { AppLog.Write($"Santé des disques physiques indisponible : {ex.Message}"); }
        return result;
    }

    private static Dictionary<string, ReliabilityCounters> ReadReliabilityCounters(ManagementScope scope)
    {
        var result = new Dictionary<string, ReliabilityCounters>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher(scope,
                new ObjectQuery("SELECT DeviceId, Wear, PowerOnHours, Temperature FROM MSFT_StorageReliabilityCounter"));
            foreach (ManagementObject counter in searcher.Get())
            {
                var deviceId = counter["DeviceId"]?.ToString();
                if (string.IsNullOrWhiteSpace(deviceId)) continue;
                result[deviceId] = new ReliabilityCounters(
                    ReadNullableInt(counter, "Wear"),
                    ReadNullableInt(counter, "PowerOnHours"),
                    ReadNullableInt(counter, "Temperature"));
            }
        }
        catch (Exception ex) { AppLog.Write($"Compteurs de fiabilité des disques indisponibles : {ex.Message}"); }
        return result;
    }

    private static int? ReadNullableInt(ManagementBaseObject value, string propertyName)
    {
        var raw = value.Properties[propertyName]?.Value;
        if (raw == null) return null;
        try { return Convert.ToInt32(raw); }
        catch { return null; }
    }

    public static int RemainingHealthPercent(int wearPercent) => 100 - Math.Clamp(wearPercent, 0, 100);

    private static int ToDisplayInt(ulong value) => value > int.MaxValue ? int.MaxValue : (int)value;

    private static string FormatUlongSize(ulong bytes) =>
        TemporaryFileCleanupService.FormatSize(bytes > long.MaxValue ? long.MaxValue : (long)bytes);

    public static string HealthStatusName(ushort status) => status switch
    {
        0 => "Sain",
        1 => "Attention",
        2 => "Défaillant",
        _ => "Inconnu"
    };

    public static string MediaTypeName(ushort type) => type switch
    {
        3 => "HDD",
        4 => "SSD",
        5 => "SCM",
        _ => "Non précisé"
    };

    private sealed record ReliabilityCounters(int? Wear, int? PowerOnHours, int? Temperature);
}
