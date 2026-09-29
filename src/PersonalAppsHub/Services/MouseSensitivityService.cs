namespace PersonalAppsHub.Services;

using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

public sealed record MouseSensitivityProfile(string Name, double? Yaw, string FovReference, params string[] Aliases)
{
    public override string ToString() => Name;
}

public sealed record ProfessionalSensitivityReference(
    double MedianEdpi, int PlayerCount, string SourceName, string SourceUrl, string UpdatedText);

public sealed class MouseSensitivityService
{
    private static readonly IReadOnlyDictionary<string, ProfessionalSensitivityReference> ProfessionalReferences =
        new Dictionary<string, ProfessionalSensitivityReference>(StringComparer.OrdinalIgnoreCase)
        {
            ["Valorant"] = new(240, 698, "ProSettings", "https://prosettings.net/guides/valorant-options/", "septembre 2026"),
            ["Counter-Strike 2"] = new(830, 940, "ProSettings", "https://prosettings.net/guides/cs2-options/", "septembre 2026"),
            ["Counter-Strike: Global Offensive"] = new(830, 940, "ProSettings (référence CS2)", "https://prosettings.net/guides/cs2-options/", "septembre 2026")
        };

    private static readonly MouseSensitivityProfile[] Profiles =
    {
        new("Valorant", 0.07, "103° horizontal (16:9, fixe)", "valorant"),
        new("Counter-Strike 2", 0.022, "≈ 106,3° horizontal à FOV 90 (16:9)", "counter-strike 2", "counter strike 2", "cs2"),
        new("Counter-Strike: Global Offensive", 0.022, "≈ 106,3° horizontal à FOV 90 (16:9)", "counter-strike global offensive", "csgo"),
        new("Apex Legends", 0.022, "Dépend du FOV choisi dans le jeu", "apex legends"),
        new("Overwatch 2", 0.0066, "103° horizontal au réglage maximal", "overwatch 2", "overwatch"),
        new("Rainbow Six Siege", 0.02, "Dépend du FOV et du format d’image", "tom clancy s rainbow six siege", "rainbow six siege"),
        new("Team Fortress 2", 0.022, "Dépend du fov_desired", "team fortress 2"),
        new("Left 4 Dead 2", 0.022, "Dépend du FOV configuré", "left 4 dead 2"),
        new("Half-Life 2", 0.022, "Dépend du FOV configuré", "half life 2"),
        new("Quake Live", 0.022, "Dépend du cg_fov", "quake live"),
        new("Quake Champions", 0.022, "Dépend du FOV choisi dans le jeu", "quake champions"),
        new("Garry's Mod", 0.022, "Dépend du FOV configuré", "garry s mod", "garrys mod"),
        new("Portal 2", 0.022, "Dépend du FOV configuré", "portal 2"),
        new("Black Mesa", 0.022, "Dépend du FOV configuré", "black mesa"),
        new("Day of Defeat: Source", 0.022, "Dépend du FOV configuré", "day of defeat source"),
        new("Counter-Strike: Source", 0.022, "Dépend du FOV configuré", "counter strike source")
        ,new("WARDOGS", 0.035, "Dépend du FOV choisi dans le jeu", "wardogs", "war dogs")
    };

    public sealed record SavedSensitivity(double? Value, string Details);

    public ProfessionalSensitivityReference? GetProfessionalReference(MouseSensitivityProfile profile) =>
        ProfessionalReferences.GetValueOrDefault(profile.Name);

    public SavedSensitivity? ReadSavedSensitivity(MouseSensitivityProfile profile)
    {
        if (profile.Name == "WARDOGS")
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Wardogs", "Saved", "Config", "WindowsClient", "GameUserSettings.ini");
            if (!File.Exists(path)) return null;
            var text = File.ReadAllText(path);
            var value = ReadNumber(text, @"(?im)^MouseSensitivity=(?<value>[\d.,]+)");
            var ads = ReadNumber(text, @"(?im)^MouseADSSensitivityMultiplier=(?<value>[\d.,]+)");
            var fov = ReadNumber(text, @"(?im)^FieldOfView=(?<value>[\d.,]+)");
            var acceleration = ReadNumber(text, @"(?im)^MouseAcceleration=(?<value>[\d.,]+)");
            return new SavedSensitivity(value,
                $"WARDOGS lu en lecture seule · ADS : {Format(ads)} · FOV : {Format(fov)} · Accélération : {Format(acceleration)}");
        }

        return null;
    }

    public IReadOnlyList<MouseSensitivityProfile> FindInstalled(IEnumerable<InstalledApplicationEntry> applications)
    {
        var installedNames = applications.Select(application => Normalize(application.Name)).ToArray();
        return Profiles.Where(profile => profile.Aliases.Any(alias => installedNames.Any(installed =>
                installed.Contains(Normalize(alias), StringComparison.OrdinalIgnoreCase))))
            .OrderBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private static string Normalize(string value)
    {
        var simplified = new string(value.ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ').ToArray());
        return " " + string.Join(' ', simplified.Split(' ', StringSplitOptions.RemoveEmptyEntries)) + " ";
    }

    private static double? ReadNumber(string text, string pattern)
    {
        var match = Regex.Match(text, pattern);
        if (!match.Success) return null;
        return double.TryParse(match.Groups["value"].Value.Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static string Format(double? value) => value?.ToString("0.####", CultureInfo.CurrentCulture) ?? "inconnu";
}
