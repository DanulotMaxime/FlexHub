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

var changelog = ChangelogService.Parse(["# Notes", "## 1.0.0", "", "- Première version"]);
Check(changelog.Any(note => note.Version == "1.0.0" && note.Notes.Contains("Première version")), "lecture des patchnotes");

if (failures.Count > 0)
{
    Console.Error.WriteLine("Échecs : " + string.Join(", ", failures));
    return 1;
}

Console.WriteLine("Tous les tests de contrôle ont réussi.");
return 0;
