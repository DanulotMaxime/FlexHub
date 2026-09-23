using PersonalAppsHub;
using PersonalAppsHub.Models;
using PersonalAppsHub.Services;
using System.Buffers.Binary;

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
Check(!defaults.ReminderEnabled, "rappel Top-Serveurs désactivé par défaut");
Check(!defaults.AutoFrenchKeyboardInDialogs, "clavier de saisie désactivé par défaut");
Check(defaults.TranslatorProvider == "MyMemory", "MyMemory présélectionné pour la traduction");
Check(defaults.TranslationSourceLanguage == "EN" && defaults.TranslationTargetLanguage == "FR",
    "traduction par défaut de l’anglais vers le français");
Check(!defaults.GeminiSetupCompleted, "configuration Gemini demandée au premier lancement");
Check(!defaults.XmpMonitorEnabled && !defaults.MemorySetupCompleted, "surveillance désactivée avant l’assistant");
Check(!defaults.AiPrivacyConsentAccepted, "consentement API explicite");
Check(defaults.MonitoringEnabled, "monitoring activé par défaut");
Check(defaults.MonitoringAlertsEnabled && defaults.MonitoringCpuAlertPercent == 95 &&
      defaults.MonitoringRamAlertPercent == 90 && defaults.MonitoringGpuTemperatureAlertC == 85,
    "seuils de monitoring prudents par défaut");
Check(defaults.MonitoringCpuTemperatureAlertC == 90, "seuil de température CPU prudent par défaut");
Check(Math.Abs(SystemMonitoringService.Percentage(25, 100) - 25) < 0.01, "calcul de pourcentage monitoring");
Check(MainWindow.TryReadThreshold("85", 50, 100, out var threshold) && threshold == 85 &&
      !MainWindow.TryReadThreshold("120", 50, 100, out _), "validation des seuils monitoring");
var temporaryRoot = Path.Combine(Path.GetTempPath(), "flexhub-test-root");
Check(TemporaryFileCleanupService.IsWithinRoot(Path.Combine(temporaryRoot, "sub", "file.tmp"), temporaryRoot),
    "validation d’un fichier dans le dossier temporaire");
Check(!TemporaryFileCleanupService.IsWithinRoot(Path.Combine(Path.GetTempPath(), "outside.tmp"), temporaryRoot),
    "refus d’un fichier hors du dossier temporaire");
Check(TemporaryFileCleanupService.FormatSize(2 * 1024 * 1024).Contains("2,0") ||
      TemporaryFileCleanupService.FormatSize(2 * 1024 * 1024).Contains("2.0"), "formatage de la taille temporaire");
Check(StorageHealthService.HealthStatusName(0) == "Sain" &&
      StorageHealthService.HealthStatusName(2) == "Défaillant", "interprétation de la santé du disque");
Check(StorageHealthService.MediaTypeName(4) == "SSD" && StorageHealthService.MediaTypeName(3) == "HDD",
    "interprétation du type de disque");
Check(StorageHealthService.RemainingHealthPercent(3) == 97 &&
      StorageHealthService.RemainingHealthPercent(120) == 0, "calcul de santé restante du SSD");
var nvmeLog = new byte[512];
BinaryPrimitives.WriteUInt16LittleEndian(nvmeLog.AsSpan(1, 2), 300);
nvmeLog[5] = 4;
BinaryPrimitives.WriteUInt64LittleEndian(nvmeLog.AsSpan(32, 8), 2);
BinaryPrimitives.WriteUInt64LittleEndian(nvmeLog.AsSpan(48, 8), 3);
BinaryPrimitives.WriteUInt64LittleEndian(nvmeLog.AsSpan(112, 8), 42);
BinaryPrimitives.WriteUInt64LittleEndian(nvmeLog.AsSpan(128, 8), 1234);
BinaryPrimitives.WriteUInt64LittleEndian(nvmeLog.AsSpan(144, 8), 5);
BinaryPrimitives.WriteUInt64LittleEndian(nvmeLog.AsSpan(160, 8), 1);
var nvmeSmart = NvmeSmartService.ParseHealthLog(nvmeLog);
Check(nvmeSmart.TemperatureC == 27 && nvmeSmart.PercentageUsed == 4 && nvmeSmart.PowerOnHours == 1234,
    "décodage température, usure et heures NVMe");
Check(nvmeSmart.PowerCycles == 42 && nvmeSmart.UnsafeShutdowns == 5 && nvmeSmart.MediaErrors == 1 &&
      nvmeSmart.DataReadBytes == 1_024_000 && nvmeSmart.DataWrittenBytes == 1_536_000,
    "décodage des compteurs SMART NVMe");
var monitoringSample = await new SystemMonitoringService().CaptureAsync();
Check(monitoringSample.RamTotalGb > 0 && monitoringSample.RamPercent is >= 0 and <= 100,
    "lecture des métriques mémoire Windows");

Check(defaults.ReformulatorEnabled && defaults.SimplifierEnabled, "transformations de texte activées par défaut");
var reformulateInstruction = ResponseGenerationService.BuildTransformationInstruction(TextTransformationMode.Reformulate, "Discours médiéval");
Check(reformulateInstruction.Contains("Discours médiéval") && reformulateInstruction.Contains("sens"), "consigne de reformulation avec style");
var simplifyInstruction = ResponseGenerationService.BuildTransformationInstruction(TextTransformationMode.Simplify, "Ultra simplifié");
Check(simplifyInstruction.Contains("enfant") && simplifyInstruction.Contains("même langue"), "consigne de simplification selon le niveau");
var summaryInstruction = ResponseGenerationService.BuildTransformationInstruction(TextTransformationMode.SummarizeConversation, "5 lignes. Extrais les tâches.");
Check(summaryInstruction.Contains("5 lignes") && summaryInstruction.Contains("tâches") && summaryInstruction.Contains("même langue"), "consigne de résumé de conversation");
var definitionInstruction = ResponseGenerationService.BuildTransformationInstruction(TextTransformationMode.DefineWord, "Définition courte avec un exemple.");
Check(definitionInstruction.Contains("Définition courte") && definitionInstruction.Contains("contexte"), "consigne de définition d’un mot");

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
