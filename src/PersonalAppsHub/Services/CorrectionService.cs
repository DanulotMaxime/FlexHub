using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Documents;

namespace PersonalAppsHub.Services;

public sealed class CorrectionService
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public string CorrectLocal(string input, string language)
    {
        var box = new System.Windows.Controls.TextBox { Text = input, Language = XmlLanguage.GetLanguage(language) };
        SpellCheck.SetIsEnabled(box, true);
        box.ApplyTemplate();
        var position = 0;
        while (position < box.Text.Length)
        {
            var start = box.GetNextSpellingErrorCharacterIndex(position, LogicalDirection.Forward);
            if (start < 0) break;
            var error = box.GetSpellingError(start);
            var length = box.GetSpellingErrorLength(start);
            var suggestion = error?.Suggestions.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(suggestion)) { box.Select(start, length); box.SelectedText = suggestion; position = start + suggestion.Length; }
            else position = start + Math.Max(1, length);
        }
        return box.Text;
    }

    public async Task<string> CorrectOpenAiAsync(string input, string key, string model, string language)
    {
        var body = new
        {
            model,
            store = false,
            instructions = $"Corrige uniquement l'orthographe, la grammaire et la ponctuation du texte en {language}. Préserve strictement le sens, le ton, les retours à la ligne et le niveau de politesse. Ne commente pas.",
            input,
            text = new { format = new { type = "json_schema", name = "correction", strict = true, schema = new { type = "object", properties = new { corrected_text = new { type = "string" } }, required = new[] { "corrected_text" }, additionalProperties = false } } }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"OpenAI : {(int)response.StatusCode} {ExtractError(json)}");
        using var document = JsonDocument.Parse(json);
        foreach (var item in document.RootElement.GetProperty("output").EnumerateArray())
        foreach (var content in item.GetProperty("content").EnumerateArray())
        if (content.TryGetProperty("text", out var text))
            return JsonDocument.Parse(text.GetString()!).RootElement.GetProperty("corrected_text").GetString()!;
        throw new InvalidOperationException("La réponse OpenAI ne contient aucune correction.");
    }

    public async Task<string> CorrectGeminiAsync(string input, string key, string model, string language)
    {
        var body = new
        {
            contents = new[] { new { parts = new[] { new { text = $"Corrige uniquement l'orthographe, la grammaire et la ponctuation de ce texte en {language}. Préserve le sens, le ton et les retours à la ligne. Renvoie uniquement le texte corrigé.\n\n{input}" } } } },
            generationConfig = new
            {
                temperature = 0.1,
                maxOutputTokens = 2048,
                responseMimeType = "application/json",
                responseSchema = new { type = "OBJECT", properties = new { corrected_text = new { type = "STRING" } }, required = new[] { "corrected_text" } }
            }
        };
        var payload = JsonSerializer.Serialize(body);
        var fallbackModel = "gemini-3.5-flash-lite";
        Exception? lastError = null;
        using var totalTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var selectedModel = attempt == 0 ? model : fallbackModel;
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(selectedModel)}:generateContent";
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("x-goog-api-key", key);
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                using var response = await _http.SendAsync(request, totalTimeout.Token);
                var json = await response.Content.ReadAsStringAsync();
                if (response.IsSuccessStatusCode)
                {
                    using var document = JsonDocument.Parse(json);
                    var raw = document.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()!;
                    return JsonDocument.Parse(raw).RootElement.GetProperty("corrected_text").GetString()!;
                }

                var transient = (int)response.StatusCode is 408 or 429 or >= 500;
                lastError = new InvalidOperationException($"Gemini ({selectedModel}) : {(int)response.StatusCode} {ExtractError(json)}");
                if (!transient) throw lastError;
            }
            catch (TaskCanceledException ex)
            {
                lastError = new TimeoutException($"Gemini ({selectedModel}) n’a pas répondu dans le délai prévu.", ex);
                if (totalTimeout.IsCancellationRequested) break;
            }

            if (attempt < 2)
            {
                var delay = (int)Math.Pow(2, attempt) * 1000 + Random.Shared.Next(150, 650);
                try { await Task.Delay(delay, totalTimeout.Token); }
                catch (TaskCanceledException) { break; }
            }
        }

        throw new InvalidOperationException("Gemini n’a pas répondu en moins de 20 secondes, y compris avec le modèle Flash-Lite de secours.", lastError);
    }

    public async Task<string> CorrectLanguageToolAsync(string input, string serverUrl, string language)
    {
        var endpoint = $"{serverUrl.TrimEnd('/')}/v2/check";
        using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["text"] = input, ["language"] = language });
        using var response = await _http.PostAsync(endpoint, content);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"LanguageTool : {(int)response.StatusCode}");
        using var document = JsonDocument.Parse(json);
        var replacements = new List<(int Offset, int Length, string Value)>();
        foreach (var match in document.RootElement.GetProperty("matches").EnumerateArray())
        {
            var suggestions = match.GetProperty("replacements");
            if (suggestions.GetArrayLength() == 0) continue;
            replacements.Add((match.GetProperty("offset").GetInt32(), match.GetProperty("length").GetInt32(), suggestions[0].GetProperty("value").GetString()!));
        }
        var result = input;
        foreach (var replacement in replacements.OrderByDescending(item => item.Offset))
            result = result.Remove(replacement.Offset, replacement.Length).Insert(replacement.Offset, replacement.Value);
        return result;
    }

    private static string ExtractError(string json)
    {
        try { return JsonDocument.Parse(json).RootElement.GetProperty("error").GetProperty("message").GetString() ?? "Erreur inconnue"; }
        catch { return "Erreur inconnue"; }
    }
}
