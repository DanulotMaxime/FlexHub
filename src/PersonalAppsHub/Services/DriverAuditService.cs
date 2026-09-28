using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PersonalAppsHub.Services;

public sealed record DriverAuditEntry(
    string Category,
    string DeviceName,
    string Manufacturer,
    string DriverVersion,
    string DriverDate,
    string InfName,
    string SupportUrl);

public sealed class DriverAuditService
{
    private const string NvidiaUrl = "https://www.nvidia.com/Download/index.aspx";
    private const string AmdUrl = "https://www.amd.com/en/support.html";
    private const string IntelUrl = "https://www.intel.com/content/www/us/en/download-center/home.html";
    private const string RealtekUrl = "https://www.realtek.com/Download/Index?menu_id=297";
    private const string WindowsUpdateUrl = "ms-settings:windowsupdate";

    static DriverAuditService() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public async Task<IReadOnlyList<DriverAuditEntry>> ScanAsync()
    {
        var entries = new List<DriverAuditEntry>();
        entries.AddRange(ParsePnpUtil(await RunPnpUtilAsync("Display"), "GPU"));
        entries.AddRange(ParsePnpUtil(await RunPnpUtilAsync("Media"), "Audio")
            .Where(entry => ContainsAny(entry.DeviceName, "audio", "sound", "son", "high definition", "realtek", "nvidia", "amd")));
        entries.AddRange(ParsePnpUtil(await RunPnpUtilAsync("System"), "Chipset")
            .Where(entry => ContainsAny(entry.Manufacturer, "intel", "amd", "advanced micro devices") &&
                            ContainsAny(entry.DeviceName, "chipset", "smbus", "pci", "management engine", "gpio", "i2c", "system", "processor", "amd")));

        return entries
            .GroupBy(entry => $"{entry.Category}|{entry.DeviceName}|{entry.DriverVersion}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(entry => CategoryRank(entry.Category))
            .ThenBy(entry => entry.DeviceName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static async Task<string> RunPnpUtilAsync(string deviceClass)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pnputil.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage),
            StandardErrorEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage)
        };
        startInfo.ArgumentList.Add("/enum-devices");
        startInfo.ArgumentList.Add("/connected");
        startInfo.ArgumentList.Add("/class");
        startInfo.ArgumentList.Add(deviceClass);
        startInfo.ArgumentList.Add("/drivers");

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("L’utilitaire Windows des pilotes n’a pas pu démarrer.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "Windows n’a pas fourni la liste des pilotes." : error.Trim());
        return output;
    }

    private static IEnumerable<DriverAuditEntry> ParsePnpUtil(string output, string category)
    {
        var blocks = Regex.Split(output, @"(?im)^(?=\s*(?:Instance ID|ID d.instance)\s*:)" );
        foreach (var block in blocks)
        {
            var name = ReadLabel(block, "Device Description", "Description de l'appareil");
            if (string.IsNullOrWhiteSpace(name)) continue;
            var manufacturer = ReadLabel(block, "Manufacturer Name", "Nom du fabricant");
            var inf = ReadLabel(block, "Driver Name", "Nom du pilote");
            var versionMatch = Regex.Match(block, @"(?im)^\s*(?:Driver Version|Version du pilote)\s*:\s*(?<date>\d{2}/\d{2}/\d{4})\s+(?<version>[\d.]+)");
            yield return new DriverAuditEntry(category, name, ValueOrUnknown(manufacturer),
                versionMatch.Success ? versionMatch.Groups["version"].Value : "Non communiqué",
                versionMatch.Success ? versionMatch.Groups["date"].Value : "Non communiquée",
                ValueOrUnknown(inf), SupportUrlFor(manufacturer, name));
        }
    }

    private static string ReadLabel(string block, params string[] labels)
    {
        foreach (var label in labels)
        {
            var match = Regex.Match(block, $@"(?im)^\s*{Regex.Escape(label)}[^:]*:\s*(?<value>.+?)\s*$", RegexOptions.CultureInvariant);
            if (match.Success) return match.Groups["value"].Value.Trim();
        }
        return "";
    }

    private static string SupportUrlFor(string manufacturer, string name)
    {
        var value = $"{manufacturer} {name}";
        if (ContainsAny(value, "nvidia")) return NvidiaUrl;
        if (ContainsAny(value, "advanced micro devices", "amd")) return AmdUrl;
        if (ContainsAny(value, "intel")) return IntelUrl;
        if (ContainsAny(value, "realtek")) return RealtekUrl;
        return WindowsUpdateUrl;
    }

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static string ValueOrUnknown(string value) => string.IsNullOrWhiteSpace(value) ? "Non communiqué" : value;
    private static int CategoryRank(string category) => category == "GPU" ? 0 : category == "Audio" ? 1 : 2;
}
