using System.Diagnostics;
using System.IO;
using System.Xml.Linq;
using System.Xml;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;

namespace PersonalAppsHub.Services;

public sealed class NvidiaProfileService
{
    private const string ExpectedToolSha256 = "1EBD8129B3C564BF226291FB3344819FD59668066F0C5E03334A69A04A62859E";
    public string BaselinePath => Path.Combine(AppContext.BaseDirectory, "Profiles", "NVIDIA-Global-Profile-Baseline.nip");
    private string ProfilesFolder => Path.Combine(AppContext.BaseDirectory, "Profiles");
    private string ToolPath => Path.Combine(AppContext.BaseDirectory, "Tools", "nvidiaProfileInspector.exe");
    private string ReferencePath => Path.Combine(AppContext.BaseDirectory, "Tools", "Reference.xml");
    public string UserProfilesFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PersonalAppsHub", "NvidiaProfiles");
    public string BeforeOptimizationPath => Path.Combine(UserProfilesFolder, "Dernier-profil-avant-optimisation.nip");

    private string? DetectedProfileKey => DetectProfileKey();
    public string? DetectedGpuGroup => DetectedProfileKey?.Replace("RTX-", "RTX ").Replace('-', '/');
    public string? OptimizationPath => DetectedProfileKey is { } key ? Path.Combine(ProfilesFolder, $"NVIDIA-Optimization-{key}.nip") : null;
    public bool FilesAvailable => File.Exists(ToolPath) && File.Exists(BaselinePath) && IsToolIntegrityValid();
    public bool CanOptimize => OptimizationPath is { } path && File.Exists(path);

    public string GetOptimizationSummary()
    {
        if (OptimizationPath is not { } path || !File.Exists(path))
            return "Aucun profil compatible avec cette carte graphique.";
        try
        {
            var document = LoadNvidiaProfile(path);
            var valueNames = LoadHumanReadableValues();
            var profile = document.Root?.Elements("Profile").FirstOrDefault();
            var settings = profile?.Element("Settings")?.Elements("ProfileSetting").ToArray() ?? [];
            var lines = settings.Select(setting =>
            {
                var name = ((string?)setting.Element("SettingNameInfo"))?.Trim();
                var id = (string?)setting.Element("SettingID") ?? "?";
                var value = (string?)setting.Element("SettingValue") ?? "?";
                uint.TryParse(id, out var numericId);
                uint.TryParse(value, out var numericValue);
                var readableValue = valueNames.TryGetValue((numericId, numericValue), out var label)
                    ? label
                    : DescribeValue(name, numericId, numericValue, value);
                return $"• {TranslateSettingName(name, id)}\n  → {readableValue}";
            });
            return $"PROFIL {DetectedGpuGroup}\n{settings.Length} paramètres appliqués\n\n{string.Join("\n", lines)}";
        }
        catch (Exception ex) { return $"Impossible de lire le profil : {ex.Message}"; }
    }

    private Dictionary<(uint SettingId, uint Value), string> LoadHumanReadableValues()
    {
        var result = new Dictionary<(uint, uint), string>();
        if (!File.Exists(ReferencePath)) return result;
        var reference = XDocument.Load(ReferencePath);
        foreach (var setting in reference.Descendants("CustomSetting"))
        {
            if (!TryParseHex((string?)setting.Element("HexSettingID"), out var settingId)) continue;
            foreach (var value in setting.Descendants("CustomSettingValue"))
            {
                if (!TryParseHex((string?)value.Element("HexValue"), out var numericValue)) continue;
                var label = ((string?)value.Element("UserfriendlyName"))?.Trim();
                if (!string.IsNullOrWhiteSpace(label)) result[(settingId, numericValue)] = label;
            }
        }
        return result;
    }

    private static bool TryParseHex(string? value, out uint result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var text = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
        return uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out result);
    }

    private static string TranslateSettingName(string? name, string id)
    {
        if (string.IsNullOrWhiteSpace(name)) return $"Paramètre de compatibilité NVIDIA ({id})";
        return name.Trim() switch
        {
            "Texture filtering - Trilinear optimization" => "Filtrage des textures — optimisation trilinéaire",
            "Vertical Sync Tear Control" => "Contrôle du déchirement d’image",
            "Preferred refresh rate" => "Fréquence de rafraîchissement préférée",
            "Maximum pre-rendered frames" => "Nombre maximal d’images pré-rendues",
            "Enable DeepDVC Feature" => "RTX Dynamic Vibrance",
            "Vertical Sync" => "Synchronisation verticale",
            "Saturation value for DeepDVC" => "Saturation RTX Dynamic Vibrance",
            "Intensity value for DeepDVC" => "Intensité RTX Dynamic Vibrance",
            "Shader disk cache maximum size" => "Taille maximale du cache des shaders",
            "Texture filtering - Quality" => "Qualité du filtrage des textures",
            "Enable TrueHDR Feature" => "RTX HDR",
            "TrueHDR Middle Grey" => "RTX HDR — gris moyen",
            "TrueHDR contrast Control" => "RTX HDR — contraste",
            "TrueHDR Saturation Control" => "RTX HDR — saturation",
            "Texture filtering - Anisotropic sample optimization" => "Filtrage anisotrope — optimisation des échantillons",
            "Power management mode" => "Mode de gestion de l’alimentation",
            "FRL Low Latency" => "Limiteur d’images — faible latence",
            "Frame Rate Limiter" => "Limiteur d’images par seconde",
            "Frame Rate Limiter for NVCPL" => "Limiteur d’images NVIDIA App",
            "VRR requested state" => "Fréquence de rafraîchissement variable (VRR)",
            "Enable G-SYNC globally" => "G-SYNC global",
            "CUDA Sysmem Fallback Policy" => "Politique de secours mémoire système CUDA",
            "Pre-Compile Shader Options" => "Précompilation des shaders",
            var text when text.Contains("DLSSG", StringComparison.OrdinalIgnoreCase) => text.Replace("Override", "Remplacement").Replace("mode", "du mode"),
            var text when text.Contains("DLSS", StringComparison.OrdinalIgnoreCase) => text.Replace("Override", "Remplacement").Replace("Enable", "Activation"),
            _ => name.Trim()
        };
    }

    private static string DescribeValue(string? name, uint id, uint value, string raw)
    {
        var text = name ?? "";
        if (id == 11306135 && value == uint.MaxValue) return "Illimitée";
        if (id == 13510289 && value == 20) return "Haute performance";
        if (id == 6600001 && value == 1) return "La plus élevée disponible";
        if (id == 8102046) return $"{value} image" + (value > 1 ? "s" : "");
        if (id == 274197361 && value == 1) return "Privilégier les performances maximales";
        if (id is 277041154 or 277041162) return $"{value} FPS";
        if (id == 11041231 && value == 0x47814940) return "Activée";
        if (text.Contains("Saturation", StringComparison.OrdinalIgnoreCase) || text.Contains("Intensity", StringComparison.OrdinalIgnoreCase) || text.Contains("Middle Grey", StringComparison.OrdinalIgnoreCase) || text.Contains("contrast", StringComparison.OrdinalIgnoreCase)) return $"{value} %";
        if ((text.Contains("Enable", StringComparison.OrdinalIgnoreCase) || text.Contains("G-SYNC", StringComparison.OrdinalIgnoreCase) || text.Contains("VRR", StringComparison.OrdinalIgnoreCase) || text.Contains("Low Latency", StringComparison.OrdinalIgnoreCase) || text.Contains("optimization", StringComparison.OrdinalIgnoreCase)) && value <= 1) return value == 1 ? "Activé" : "Désactivé";
        return $"Réglage NVIDIA conservé (valeur technique : {raw})";
    }
    public bool CanRestore => File.Exists(BeforeOptimizationPath);

    public Task ApplyOptimizationAsync() => ImportAsync(OptimizationPath ?? throw new InvalidOperationException("Aucun profil compatible avec cette carte graphique."));
    public Task RestorePreviousAsync() => ImportAsync(BeforeOptimizationPath);
    public Task ApplyProfileAsync(NvidiaSavedProfile profile) => ImportAsync(profile.Path);

    public async Task<NvidiaProfileSnapshot> BackupThenOptimizeAsync()
    {
        var backup = await SaveCurrentProfileAsync("Dernier-profil-avant-optimisation", BeforeOptimizationPath);
        ArchiveBackup(backup.Path);
        await ApplyOptimizationAsync();
        return backup;
    }

    public async Task<NvidiaProfileSnapshot> BackupThenApplyAsync(NvidiaSavedProfile profile)
    {
        var backup = await SaveCurrentProfileAsync("Dernier-profil-avant-optimisation", BeforeOptimizationPath);
        ArchiveBackup(backup.Path);
        await ApplyProfileAsync(profile);
        return backup;
    }

    private void ArchiveBackup(string sourcePath)
    {
        var archivePath = Path.Combine(UserProfilesFolder, $"Avant-modification-{DateTime.Now:yyyyMMdd-HHmmss}.nip");
        File.Copy(sourcePath, archivePath, overwrite: false);
    }

    public IReadOnlyList<NvidiaSavedProfile> GetSavedProfiles()
    {
        Directory.CreateDirectory(UserProfilesFolder);
        var profiles = new List<NvidiaSavedProfile>();
        if (OptimizationPath is { } optimizationPath && File.Exists(optimizationPath))
            profiles.Add(new($"Profil d’optimisation {DetectedGpuGroup}", optimizationPath, true));
        profiles.AddRange(new DirectoryInfo(UserProfilesFolder).GetFiles("*.nip")
            .OrderByDescending(file => file.LastWriteTime)
            .Select(file => new NvidiaSavedProfile(Path.GetFileNameWithoutExtension(file.Name), file.FullName, false)));
        return profiles;
    }

    public void DeleteProfile(NvidiaSavedProfile profile)
    {
        if (profile.IsOptimization || Path.GetFullPath(profile.Path).StartsWith(Path.GetFullPath(ProfilesFolder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Le profil d’optimisation ne peut pas être supprimé.");
        var fullPath = Path.GetFullPath(profile.Path);
        var allowedFolder = Path.GetFullPath(UserProfilesFolder) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(allowedFolder, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Ce profil ne peut pas être supprimé.");
        if (File.Exists(fullPath)) File.Delete(fullPath);
    }

    public string GetHardwareInformation()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "nvidia-smi.exe",
                Arguments = "--query-gpu=name,driver_version,vbios_version --format=csv,noheader,nounits",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            if (process == null) return "Informations NVIDIA indisponibles";
            var result = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);
            if (string.IsNullOrWhiteSpace(result)) return "Informations NVIDIA indisponibles";
            var parts = result.Split(',', StringSplitOptions.TrimEntries);
            return parts.Length >= 3 ? $"{parts[0]} · Pilote {parts[1]} · VBIOS {parts[2]} (informatif)" : result;
        }
        catch { return "Informations NVIDIA indisponibles"; }
    }

    private string? DetectProfileKey()
    {
        var information = GetHardwareInformation();
        var match = Regex.Match(information, @"RTX\s*(5090|5080|5070|5060|5050|4090|4080|4070|4060|3090|3080|3070|3060)", RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        return match.Groups[1].Value switch
        {
            "5090" => "RTX-5090",
            "5080" or "5070" => "RTX-5080-5070",
            "5060" or "5050" => "RTX-5060-5050",
            "4090" => "RTX-4090",
            "4080" or "4070" => "RTX-4080-4070",
            "4060" => "RTX-4060",
            "3090" => "RTX-3090",
            "3080" or "3070" => "RTX-3080-3070",
            "3060" => "RTX-3060",
            _ => null
        };
    }

    public Task<NvidiaProfileSnapshot> SaveCurrentProfileAsync() => SaveCurrentProfileAsync($"Profil-NVIDIA-{DateTime.Now:yyyyMMdd-HHmmss}", null);

    private async Task<NvidiaProfileSnapshot> SaveCurrentProfileAsync(string name, string? fixedPath)
    {
        VerifyToolIntegrity();
        var toolFolder = Path.GetDirectoryName(ToolPath)!;
        var started = DateTime.Now.AddSeconds(-2);
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = ToolPath,
            Arguments = "-exportCustomized",
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = toolFolder
        }) ?? throw new InvalidOperationException("Impossible de démarrer l’export NVIDIA.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException($"L’export NVIDIA a échoué (code {process.ExitCode}).");
        await Task.Delay(1200);
        var export = new DirectoryInfo(toolFolder).GetFiles("CustomProfiles_*.nip")
            .Where(file => file.LastWriteTime >= started)
            .OrderByDescending(file => file.LastWriteTime)
            .FirstOrDefault() ?? throw new InvalidOperationException("L’outil NVIDIA n’a créé aucun fichier d’export.");

        var source = LoadNvidiaProfile(export.FullName);
        var profile = source.Root?.Elements("Profile").FirstOrDefault(item => (string?)item.Element("ProfileName") == "Base Profile")
            ?? throw new InvalidOperationException("Le profil global NVIDIA est absent de l’export.");
        var output = new XDocument(new XDeclaration("1.0", "utf-16", null), new XElement("ArrayOfProfile", new XElement(profile)));
        Directory.CreateDirectory(UserProfilesFolder);
        var path = fixedPath ?? Path.Combine(UserProfilesFolder, $"{name}.nip");
        SaveUnicodeProfile(output, path);
        var count = profile.Element("Settings")?.Elements("ProfileSetting").Count() ?? 0;
        return new NvidiaProfileSnapshot(path, count);
    }

    private static XDocument LoadNvidiaProfile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        string xml;
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            xml = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            xml = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        else if (bytes.Length >= 4 && bytes[1] == 0 && bytes[3] == 0)
            xml = Encoding.Unicode.GetString(bytes);
        else if (bytes.Length >= 4 && bytes[0] == 0 && bytes[2] == 0)
            xml = Encoding.BigEndianUnicode.GetString(bytes);
        else
            xml = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');

        xml = Regex.Replace(xml, @"<\?xml[^?]*\?>", "", RegexOptions.IgnoreCase).TrimStart();
        return XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
    }

    private static void SaveUnicodeProfile(XDocument document, string path)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UnicodeEncoding(false, true),
            Indent = true,
            OmitXmlDeclaration = false
        };
        using var writer = XmlWriter.Create(path, settings);
        document.Save(writer);
    }

    private async Task ImportAsync(string profilePath)
    {
        VerifyToolIntegrity();
        if (!File.Exists(profilePath)) throw new FileNotFoundException("Le profil NVIDIA est introuvable.", profilePath);
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = ToolPath,
            Arguments = $"-silentImport \"{profilePath}\"",
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Path.GetDirectoryName(ToolPath)!
        }) ?? throw new InvalidOperationException("Impossible de démarrer l’outil NVIDIA.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0) throw new InvalidOperationException($"L’importation NVIDIA a échoué (code {process.ExitCode}).");
    }

    private bool IsToolIntegrityValid()
    {
        try
        {
            if (!File.Exists(ToolPath)) return false;
            using var stream = File.OpenRead(ToolPath);
            return Convert.ToHexString(SHA256.HashData(stream)).Equals(ExpectedToolSha256, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private void VerifyToolIntegrity()
    {
        if (!File.Exists(ToolPath)) throw new FileNotFoundException("NVIDIA Profile Inspector est introuvable.", ToolPath);
        if (!IsToolIntegrityValid())
            throw new InvalidOperationException("La signature locale de NVIDIA Profile Inspector ne correspond pas à la version vérifiée. L’opération administrateur est annulée par sécurité.");
    }

}

public sealed record NvidiaProfileSnapshot(string Path, int SettingCount);
public sealed record NvidiaSavedProfile(string Name, string Path, bool IsOptimization)
{
    public override string ToString() => Name;
}
