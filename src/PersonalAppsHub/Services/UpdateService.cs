using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace PersonalAppsHub.Services;

public sealed class UpdateService
{
    public const string GitHubRepository = "DanulotMaxime/FlexHub";
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromMinutes(10) };

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
        string? installerUrl = null;
        string? installerSha256 = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var assetName) ? assetName.GetString() : null;
                if (!string.Equals(name, "FlexHub-Setup-x64.exe", StringComparison.OrdinalIgnoreCase)) continue;
                installerUrl = asset.TryGetProperty("browser_download_url", out var downloadUrl) ? downloadUrl.GetString() : null;
                var digest = asset.TryGetProperty("digest", out var assetDigest) ? assetDigest.GetString() : null;
                installerSha256 = digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true ? digest[7..] : null;
                break;
            }
        }
        var isNewer = IsNewerVersion(CurrentVersion, tag);
        return new UpdateCheckResult(isNewer, tag, page, notes, installerUrl, installerSha256);
    }

    public async Task DownloadAndStartInstallerAsync(UpdateCheckResult result, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(result.InstallerDownloadUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("L’adresse de téléchargement de l’installeur est invalide.");
        if (string.IsNullOrWhiteSpace(result.InstallerSha256))
            throw new InvalidOperationException("GitHub n’a pas fourni l’empreinte de sécurité de l’installeur.");

        var installerPath = Path.Combine(Path.GetTempPath(), $"FlexHub-Setup-{result.LatestVersion}-{Guid.NewGuid():N}.exe");
        using (var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = new FileStream(installerPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await source.CopyToAsync(destination, cancellationToken);
        }

        await using var installer = File.OpenRead(installerPath);
        var actualHash = Convert.ToHexString(await SHA256.HashDataAsync(installer, cancellationToken));
        if (!actualHash.Equals(result.InstallerSha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(installerPath);
            throw new InvalidOperationException("L’empreinte de l’installeur téléchargé ne correspond pas à celle publiée par GitHub.");
        }

        Process.Start(new ProcessStartInfo(installerPath, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS")
        {
            UseShellExecute = true
        });
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

public sealed record UpdateCheckResult(bool IsNewer, string LatestVersion, string ReleasePageUrl, string Notes, string? InstallerDownloadUrl = null, string? InstallerSha256 = null);
