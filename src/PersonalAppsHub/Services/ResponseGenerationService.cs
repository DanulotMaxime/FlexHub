using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace PersonalAppsHub.Services;

public sealed class ResponseGenerationService
{
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(20) };

    public async Task<string> GenerateOpenAiAsync(string message, string apiKey, string model, string tone, string instruction)
    {
        var body = new
        {
            model,
            store = false,
            instructions = BuildInstruction(tone, instruction),
            input = message
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await _client.SendAsync(request);
        ApiQuotaTracker.CaptureOpenAi(response);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"OpenAI ({(int)response.StatusCode}) : {ReadError(json)}");
        using var document = JsonDocument.Parse(json);
        foreach (var item in document.RootElement.GetProperty("output").EnumerateArray())
        foreach (var content in item.GetProperty("content").EnumerateArray())
            if (content.TryGetProperty("text", out var text) && !string.IsNullOrWhiteSpace(text.GetString())) return text.GetString()!.Trim();
        throw new InvalidOperationException("OpenAI n’a généré aucune réponse.");
    }

    public async Task<string> GenerateGeminiAsync(string message, string apiKey, string model, string tone, string instruction)
    {
        var prompt = $"{BuildInstruction(tone, instruction)}\n\nMessage auquel répondre :\n{message}";
        var models = new[] { model.Trim(), "gemini-3.5-flash-lite", "gemini-2.5-flash-lite" }.Distinct(StringComparer.OrdinalIgnoreCase);
        string? lastError = null;
        foreach (var candidate in models)
        {
            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(candidate)}:generateContent";
            var body = new { contents = new[] { new { parts = new[] { new { text = prompt } } } }, generationConfig = new { temperature = 0.55, maxOutputTokens = 2048 } };
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
            return document.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()?.Trim()
                ?? throw new InvalidOperationException("Gemini n’a généré aucune réponse.");
        }
        throw new InvalidOperationException(lastError ?? "Aucun modèle Gemini compatible n’a répondu.");
    }

    private static string BuildInstruction(string tone, string instruction) =>
        $"Rédige uniquement une réponse prête à envoyer au message fourni. Ton : {tone}. N’ajoute ni explication, ni titre, ni guillemets. {instruction}";

    private static string ReadError(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var error = document.RootElement.GetProperty("error");
            return error.TryGetProperty("message", out var message) ? message.GetString() ?? "Erreur inconnue" : error.ToString();
        }
        catch { return json.Length > 400 ? json[..400] : json; }
    }
}
