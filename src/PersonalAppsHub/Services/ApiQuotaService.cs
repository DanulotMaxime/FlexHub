using System.Net.Http.Headers;
using System.Net.Http;
using System.Text.Json;

namespace PersonalAppsHub.Services;

public static class ApiQuotaTracker
{
    public static string OpenAiStatus { get; private set; } = "Disponible après la première utilisation";

    public static void CaptureOpenAi(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("x-ratelimit-remaining-requests", out var requests)) return;
        var remaining = requests.FirstOrDefault();
        var reset = response.Headers.TryGetValues("x-ratelimit-reset-requests", out var resets) ? resets.FirstOrDefault() : null;
        OpenAiStatus = string.IsNullOrWhiteSpace(reset)
            ? $"{remaining} requêtes disponibles"
            : $"{remaining} requêtes · réinitialisation dans {reset}";
    }
}

public sealed class ApiQuotaService
{
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<string> GetDeepLStatusAsync(string apiKey, bool freeApi)
    {
        var endpoint = freeApi ? "https://api-free.deepl.com/v2/usage" : "https://api.deepl.com/v2/usage";
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", apiKey);
        using var response = await _client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) return $"Quota indisponible ({(int)response.StatusCode})";

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("character_count", out var usedNode) || !root.TryGetProperty("character_limit", out var limitNode))
            return "Quota non communiqué par DeepL";

        var used = usedNode.GetInt64();
        var limit = limitNode.GetInt64();
        return $"{Math.Max(0, limit - used):N0} / {limit:N0} caractères restants";
    }
}
