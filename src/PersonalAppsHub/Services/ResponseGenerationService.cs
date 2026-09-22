using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace PersonalAppsHub.Services;

public sealed class ResponseGenerationService
{
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(70) };
    private static readonly TimeSpan GeminiTotalTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan GeminiAttemptTimeout = TimeSpan.FromMinutes(1);

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
        var models = GeminiModels(model);
        string? lastError = null;
        using var totalTimeout = new CancellationTokenSource(GeminiTotalTimeout);
        foreach (var candidate in models)
        {
            try
            {
                AppLog.Write($"GEMINI RÉPONSE | Essai du modèle {candidate}.");
                var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(candidate)}:generateContent";
                var body = new { contents = new[] { new { parts = new[] { new { text = prompt } } } }, generationConfig = new { temperature = 0.55, maxOutputTokens = 2048 } };
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Add("x-goog-api-key", apiKey);
                request.Content = JsonContent.Create(body);
                using var attemptTimeout = CancellationTokenSource.CreateLinkedTokenSource(totalTimeout.Token);
                attemptTimeout.CancelAfter(GeminiAttemptTimeout);
                using var response = await _client.SendAsync(request, attemptTimeout.Token);
                var json = await response.Content.ReadAsStringAsync(totalTimeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    lastError = $"Gemini ({candidate}, {(int)response.StatusCode}) : {ReadError(json)}";
                    AppLog.Write($"GEMINI RÉPONSE | {lastError}");
                    if ((int)response.StatusCode is 408 or 404 or 429 or >= 500) continue;
                    throw new InvalidOperationException(lastError);
                }
                using var document = JsonDocument.Parse(json);
                var result = document.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()?.Trim()
                    ?? throw new InvalidOperationException("Gemini n’a généré aucune réponse.");
                AppLog.Write($"GEMINI RÉPONSE | Modèle {candidate} réussi.");
                return result;
            }
            catch (TaskCanceledException) when (!totalTimeout.IsCancellationRequested)
            {
                lastError = $"Gemini ({candidate}) n’a pas répondu en {GeminiAttemptTimeout.TotalSeconds:0} secondes.";
                AppLog.Write($"GEMINI RÉPONSE | {lastError} Essai du modèle suivant.");
            }
        }
        throw new InvalidOperationException(lastError ?? "Aucun modèle Gemini compatible n’a répondu dans le délai autorisé.");
    }

    public Task<string> TransformOpenAiAsync(string text, string apiKey, string model, TextTransformationMode mode, string option)
    {
        var instruction = BuildTransformationInstruction(mode, option);
        return GenerateOpenAiTransformationAsync(text, apiKey, model, instruction);
    }

    public Task<string> TransformGeminiAsync(string text, string apiKey, string model, TextTransformationMode mode, string option)
    {
        var instruction = BuildTransformationInstruction(mode, option);
        return GenerateGeminiTransformationAsync(text, apiKey, model, instruction);
    }

    public static string BuildTransformationInstruction(TextTransformationMode mode, string option) => mode switch
    {
        TextTransformationMode.Reformulate =>
            $"Reformule le texte fourni dans le style suivant : {option}. Adapte le vocabulaire, le rythme et le niveau de formalité à ce style. " +
            "Conserve exactement son sens, sa langue, ses faits et sa mise en forme utile. " +
            "Améliore la fluidité et la clarté sans inventer d’information. Réponds uniquement avec le texte reformulé, sans titre, explication ni guillemets.",
        TextTransformationMode.Simplify =>
            BuildSimplificationInstruction(option),
        TextTransformationMode.SummarizeConversation =>
            $"Résume la conversation fournie dans la même langue. {option} " +
            "Reste fidèle aux échanges, ne crée aucune information et réponds uniquement avec le résumé demandé, sans préambule ni commentaire.",
        TextTransformationMode.DefineWord =>
            $"Définis le mot ou l’expression fourni dans sa langue d’origine. {option} " +
            "Si le texte contient une phrase, utilise-la uniquement pour déterminer le sens du mot dans son contexte. Réponds directement avec la définition, sans préambule.",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    private static string BuildSimplificationInstruction(string level)
    {
        var levelInstruction = level switch
        {
            "Ultra simplifié" => "Explique comme à un enfant : vocabulaire très courant, phrases très courtes, une idée à la fois et exemples simples si utiles.",
            "Beaucoup simplifié" => "Utilise un vocabulaire courant, des phrases courtes et explique brièvement les termes techniques indispensables.",
            _ => "Allège le vocabulaire et les phrases tout en conservant un niveau adapté à un adulte non spécialiste."
        };
        return $"Réécris le texte fourni dans la même langue pour une personne qui ne connaît pas le sujet. Niveau demandé : {level}. {levelInstruction} " +
            "Conserve tous les faits importants et la mise en forme utile. Réponds uniquement avec le texte simplifié, sans titre, explication ni guillemets.";
    }

    private async Task<string> GenerateOpenAiTransformationAsync(string text, string apiKey, string model, string instruction)
    {
        var body = new { model, store = false, instructions = instruction, input = text };
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
            if (content.TryGetProperty("text", out var output) && !string.IsNullOrWhiteSpace(output.GetString())) return output.GetString()!.Trim();
        throw new InvalidOperationException("OpenAI n’a généré aucun texte.");
    }

    private async Task<string> GenerateGeminiTransformationAsync(string text, string apiKey, string model, string instruction)
    {
        var prompt = $"{instruction}\n\nTexte à transformer :\n{text}";
        var models = GeminiModels(model);
        string? lastError = null;
        using var totalTimeout = new CancellationTokenSource(GeminiTotalTimeout);
        foreach (var candidate in models)
        {
            try
            {
                AppLog.Write($"GEMINI TRANSFORMATION | Essai du modèle {candidate}.");
                var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(candidate)}:generateContent";
                var body = new { contents = new[] { new { parts = new[] { new { text = prompt } } } }, generationConfig = new { temperature = 0.35, maxOutputTokens = 2048 } };
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Add("x-goog-api-key", apiKey);
                request.Content = JsonContent.Create(body);
                using var attemptTimeout = CancellationTokenSource.CreateLinkedTokenSource(totalTimeout.Token);
                attemptTimeout.CancelAfter(GeminiAttemptTimeout);
                using var response = await _client.SendAsync(request, attemptTimeout.Token);
                var json = await response.Content.ReadAsStringAsync(totalTimeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    lastError = $"Gemini ({candidate}, {(int)response.StatusCode}) : {ReadError(json)}";
                    AppLog.Write($"GEMINI TRANSFORMATION | {lastError}");
                    if ((int)response.StatusCode is 408 or 404 or 429 or >= 500) continue;
                    throw new InvalidOperationException(lastError);
                }
                using var document = JsonDocument.Parse(json);
                var result = document.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()?.Trim()
                    ?? throw new InvalidOperationException("Gemini n’a généré aucun texte.");
                AppLog.Write($"GEMINI TRANSFORMATION | Modèle {candidate} réussi.");
                return result;
            }
            catch (TaskCanceledException) when (!totalTimeout.IsCancellationRequested)
            {
                lastError = $"Gemini ({candidate}) n’a pas répondu en {GeminiAttemptTimeout.TotalSeconds:0} secondes.";
                AppLog.Write($"GEMINI TRANSFORMATION | {lastError} Essai du modèle suivant.");
            }
        }
        throw new InvalidOperationException(lastError ?? "Aucun modèle Gemini compatible n’a répondu dans le délai autorisé.");
    }

    private static IEnumerable<string> GeminiModels(string configuredModel) =>
        new[] { "gemini-3.1-flash-lite", configuredModel.Trim(), "gemini-3.5-flash" }
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Distinct(StringComparer.OrdinalIgnoreCase);

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

public enum TextTransformationMode
{
    Reformulate,
    Simplify,
    SummarizeConversation,
    DefineWord
}
