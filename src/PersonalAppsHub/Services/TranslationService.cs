using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;

namespace PersonalAppsHub.Services;

public sealed class TranslationService
{
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(20) };

    public async Task<string> TranslateDeepLAsync(string text, string apiKey, string targetLanguage, string sourceLanguage, bool freeApi)
    {
        var endpoint = freeApi ? "https://api-free.deepl.com/v2/translate" : "https://api.deepl.com/v2/translate";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", apiKey);
        var body = new Dictionary<string, object> { ["text"] = new[] { text }, ["target_lang"] = targetLanguage };
        if (!sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase)) body["source_lang"] = sourceLanguage;
        request.Content = JsonContent.Create(body);
        using var response = await _client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"DeepL ({(int)response.StatusCode}) : {ReadError(json)}");
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("translations")[0].GetProperty("text").GetString() ?? text;
    }

    public async Task<string> TranslateGoogleAsync(string text, string apiKey, string targetLanguage, string sourceLanguage)
    {
        const string endpoint = "https://translation.googleapis.com/language/translate/v2";
        var body = new Dictionary<string, object> { ["q"] = text, ["target"] = targetLanguage.ToLowerInvariant(), ["format"] = "text" };
        if (!sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase)) body["source"] = sourceLanguage.ToLowerInvariant();
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = JsonContent.Create(body);
        using var response = await _client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Google Traduction ({(int)response.StatusCode}) : {ReadError(json)}");
        using var document = JsonDocument.Parse(json);
        var translated = document.RootElement.GetProperty("data").GetProperty("translations")[0].GetProperty("translatedText").GetString() ?? text;
        return WebUtility.HtmlDecode(translated);
    }

    public async Task<string> TranslateGeminiAsync(string text, string apiKey, string model, string targetLanguage, string sourceLanguage)
    {
        var source = sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase) ? "détectée automatiquement" : sourceLanguage;
        var prompt = $"Traduis fidèlement le texte suivant de la langue {source} vers {targetLanguage}. Conserve la mise en forme et réponds uniquement avec la traduction, sans explication :\n\n{text}";
        var models = new[] { model.Trim(), "gemini-3.5-flash-lite", "gemini-2.5-flash-lite" }.Distinct(StringComparer.OrdinalIgnoreCase);
        string? lastError = null;
        foreach (var candidateModel in models)
        {
            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(candidateModel)}:generateContent";
            var body = new { contents = new[] { new { parts = new[] { new { text = prompt } } } }, generationConfig = new { temperature = 0.1 } };
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Add("x-goog-api-key", apiKey);
            request.Content = JsonContent.Create(body);
            using var response = await _client.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                lastError = $"Gemini ({(int)response.StatusCode}) : {ReadError(json)}";
                if ((int)response.StatusCode is 404 or 429 or 503) continue;
                throw new InvalidOperationException(lastError);
            }
            using var document = JsonDocument.Parse(json);
            var result = document.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()?.Trim() ?? text;
            return RemoveMarkdownFence(result);
        }
        throw new InvalidOperationException(lastError ?? "Aucun modèle Gemini compatible n’a répondu.");
    }

    public async Task<string> TranslateLibreAsync(string text, string? apiKey, string baseUrl, string targetLanguage, string sourceLanguage)
    {
        var endpoint = $"{baseUrl.TrimEnd('/')}/translate";
        var body = new Dictionary<string, object> { ["q"] = text, ["source"] = sourceLanguage.ToLowerInvariant(), ["target"] = targetLanguage.ToLowerInvariant(), ["format"] = "text" };
        if (!string.IsNullOrWhiteSpace(apiKey)) body["api_key"] = apiKey;
        using var response = await _client.PostAsJsonAsync(endpoint, body);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"LibreTranslate ({(int)response.StatusCode}) : {ReadError(json)}");
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("translatedText").GetString() ?? text;
    }

    public async Task<string> TranslateMyMemoryAsync(string text, string targetLanguage, string sourceLanguage, string? email)
    {
        if (sourceLanguage.Equals("auto", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MyMemory nécessite de choisir une langue source.");
        var endpoint = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(text)}&langpair={Uri.EscapeDataString(sourceLanguage.ToLowerInvariant() + "|" + targetLanguage.ToLowerInvariant())}";
        if (!string.IsNullOrWhiteSpace(email)) endpoint += $"&de={Uri.EscapeDataString(email)}";
        using var response = await _client.GetAsync(endpoint);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"MyMemory ({(int)response.StatusCode}) : {ReadError(json)}");
        using var document = JsonDocument.Parse(json);
        var status = document.RootElement.TryGetProperty("responseStatus", out var statusNode) ? statusNode.GetInt32() : 200;
        if (status != 200) throw new InvalidOperationException($"MyMemory : {document.RootElement.GetProperty("responseDetails")}");
        return WebUtility.HtmlDecode(document.RootElement.GetProperty("responseData").GetProperty("translatedText").GetString() ?? text);
    }

    private static string ReadError(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("message", out var message)) return message.GetString() ?? json;
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out message)) return message.GetString() ?? json;
                return error.ToString();
            }
        }
        catch { }
        return json.Length > 400 ? json[..400] : json;
    }

    private static string RemoveMarkdownFence(string value)
    {
        if (!value.StartsWith("```", StringComparison.Ordinal)) return value;
        var firstLine = value.IndexOf('\n');
        var lastFence = value.LastIndexOf("```", StringComparison.Ordinal);
        return firstLine >= 0 && lastFence > firstLine ? value[(firstLine + 1)..lastFence].Trim() : value;
    }
}
