using PersonalAppsHub.Models;
using PersonalAppsHub.Services;

var failures = new List<string>();
void Check(bool condition, string name)
{
    if (!condition) failures.Add(name);
}

Check(UpdateService.IsNewerVersion("1.0.0", "v1.1.0"), "détection d’une mise à jour");
Check(!UpdateService.IsNewerVersion("1.2.0", "1.1.9"), "refus d’une ancienne version");
Check(!UpdateService.IsNewerVersion("1.0.0", "version-invalide"), "version invalide");

var ddr4Ok = XmpMonitorService.Evaluate([new("Module A", 3200), new("Module B", 3600)], 3200, 3600);
var ddr4Low = XmpMonitorService.Evaluate([new("Module A", 2666)], 3200, 3600);
var ddr5Fast = XmpMonitorService.Evaluate([new("Module A", 7600)], 5800, 7400);
Check(ddr4Ok.IsProbablyActive, "DDR4 dans la plage");
Check(!ddr4Low.IsProbablyActive, "DDR4 sous la plage");
Check(ddr5Fast.IsProbablyActive, "DDR5 au-dessus de la plage");

var defaults = new HubSettings();
Check(!defaults.XmpMonitorEnabled && !defaults.MemorySetupCompleted, "surveillance désactivée avant l’assistant");
Check(!defaults.AiPrivacyConsentAccepted, "consentement API explicite");

Check(NvidiaProfileService.GetProfileKeyForGpuName("NVIDIA GeForce RTX 2080 Ti") == "RTX-2080-Ti", "détection RTX 2080 Ti");
Check(NvidiaProfileService.GetProfileKeyForGpuName("NVIDIA GeForce RTX 3050 Laptop GPU") == "RTX-3050-Laptop", "détection RTX 3050 mobile");
Check(NvidiaProfileService.GetProfileKeyForGpuName("GeForce RTX 4070 Ti SUPER") == "RTX-4070-Ti-SUPER", "détection RTX 4070 Ti SUPER");
Check(NvidiaProfileService.GetProfileKeyForGpuName("GeForce RTX 5060 Ti Laptop GPU") == "RTX-5060-Ti-Laptop", "détection RTX 5060 Ti mobile");
Check(NvidiaProfileService.GetProfileKeyForGpuName("NVIDIA RTX A4000") == null, "exclusion RTX professionnelle non prise en charge");

var supportedDesktopModels = new[]
{
    "RTX-2060", "RTX-2060-SUPER", "RTX-2070", "RTX-2070-SUPER", "RTX-2080", "RTX-2080-SUPER", "RTX-2080-Ti",
    "RTX-3050", "RTX-3060", "RTX-3060-Ti", "RTX-3070", "RTX-3070-Ti", "RTX-3080", "RTX-3080-Ti", "RTX-3090", "RTX-3090-Ti",
    "RTX-4060", "RTX-4060-Ti", "RTX-4070", "RTX-4070-SUPER", "RTX-4070-Ti", "RTX-4070-Ti-SUPER", "RTX-4080", "RTX-4080-SUPER", "RTX-4090",
    "RTX-5050", "RTX-5060", "RTX-5060-Ti", "RTX-5070", "RTX-5070-Ti", "RTX-5080", "RTX-5090"
};
foreach (var expected in supportedDesktopModels)
{
    var reportedName = $"NVIDIA GeForce {expected.Replace('-', ' ')}";
    Check(NvidiaProfileService.GetProfileKeyForGpuName(reportedName) == expected, $"détection {reportedName}");
}

static IReadOnlyDictionary<uint, uint> PerformanceSettings(string key)
{
    var method = typeof(NvidiaProfileService).GetMethod("CreateMaximumPerformanceProfile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
        ?? throw new InvalidOperationException("Générateur de profil NVIDIA introuvable.");
    var document = (System.Xml.Linq.XDocument)(method.Invoke(null, [key])
        ?? throw new InvalidOperationException("Profil NVIDIA non généré."));
    return document.Descendants("ProfileSetting").ToDictionary(
        setting => uint.Parse(setting.Element("SettingID")!.Value),
        setting => uint.Parse(setting.Element("SettingValue")!.Value));
}

var rtx2060Settings = PerformanceSettings("RTX-2060");
Check(rtx2060Settings[390467] == 2, "RTX 2060 en faible latence Ultra");
Check(rtx2060Settings[13510289] == 20, "RTX 2060 en filtrage haute performance");
Check(rtx2060Settings[283385333] == 50 && rtx2060Settings[283385345] == 1, "RTX 2060 avec DLSS Performance");
Check(!rtx2060Settings.ContainsKey(283385347), "RTX 2060 sans Frame Generation incompatible");

var rtx4060Settings = PerformanceSettings("RTX-4060");
Check(rtx4060Settings[283385347] == 1, "RTX 4060 avec Frame Generation");
Check(!rtx4060Settings.ContainsKey(273507943), "RTX 4060 sans Multi Frame Generation incompatible");

var rtx5080Settings = PerformanceSettings("RTX-5080");
Check(rtx5080Settings[283385347] == 1 && rtx5080Settings[273507943] == 5, "RTX 5080 avec Multi Frame Generation maximal");

var changelog = ChangelogService.Parse(["# Notes", "## 1.0.0", "", "- Première version"]);
Check(changelog.Any(note => note.Version == "1.0.0" && note.Notes.Contains("Première version")), "lecture des patchnotes");

if (failures.Count > 0)
{
    Console.Error.WriteLine("Échecs : " + string.Join(", ", failures));
    return 1;
}

Console.WriteLine("Tous les tests de contrôle ont réussi.");
return 0;
