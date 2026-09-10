using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace PersonalAppsHub.Services;

public sealed class UpdateService
{
    public const string GitHubRepository = "DanulotMaxime/FlexHub";
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(15) };

    public static string CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
        ?? "0.0.0";

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{GitHubRepository}/releases/latest");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("FlexHub", CurrentVersion));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await _client.SendAsync(request, cancellationToken);
        if ((int)response.StatusCode == 404)
            throw new InvalidOperationException("Aucune version publique n’a été trouvée dans ce dépôt GitHub.");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString()?.TrimStart('v', 'V') ?? "0.0.0";
        var page = root.GetProperty("html_url").GetString() ?? $"https://github.com/{GitHubRepository}/releases/latest";
        var notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
        var isNewer = IsNewerVersion(CurrentVersion, tag);
        return new UpdateCheckResult(isNewer, tag, page, notes);
    }

    public static void OpenRelease(UpdateCheckResult result) =>
        Process.Start(new ProcessStartInfo(result.ReleasePageUrl) { UseShellExecute = true });

    public static bool IsNewerVersion(string currentVersion, string candidateVersion) =>
        Version.TryParse(Normalize(candidateVersion.TrimStart('v', 'V')), out var latest) &&
        Version.TryParse(Normalize(currentVersion.TrimStart('v', 'V')), out var current) &&
        latest > current;

    private static string Normalize(string version)
    {
        var stable = version.Split('-', '+')[0];
        return stable.Count(c => c == '.') == 1 ? stable + ".0" : stable;
    }
}

public sealed record UpdateCheckResult(bool IsNewer, string LatestVersion, string ReleasePageUrl, string Notes);
