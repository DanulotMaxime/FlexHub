using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PersonalAppsHub.Services;

public sealed record WiktionaryDefinition(string Word, IReadOnlyList<string> Definitions, string SourceUrl)
{
    public string DisplayText => string.Join(Environment.NewLine + Environment.NewLine,
        Definitions.Select((definition, index) => $"{index + 1}. {definition}"));
}

public sealed partial class WiktionaryService
{
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(12) };

    public WiktionaryService()
    {
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("FlexHub/1.3.1 (dictionary lookup)");
    }

    public async Task<WiktionaryDefinition?> FindAsync(string selectedText)
    {
        var word = NormalizeLookupText(selectedText);
        if (word == null) return null;
        var endpoint = "https://fr.wiktionary.org/w/api.php?action=parse&prop=wikitext&redirects=1&format=json&formatversion=2&page="
            + Uri.EscapeDataString(word);
        using var response = await _client.GetAsync(endpoint);
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("parse", out var parse) ||
            !parse.TryGetProperty("wikitext", out var wikitextNode)) return null;

        var definitions = wikitextNode.GetString()?
            .Split('\n')
            .Where(line => line.StartsWith("# ", StringComparison.Ordinal))
            .Select(line => CleanWikitext(line[2..]))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray() ?? [];
        if (definitions.Length == 0) return null;

        var sourceUrl = "https://fr.wiktionary.org/wiki/" + Uri.EscapeDataString(word.Replace(' ', '_'));
        return new WiktionaryDefinition(word, definitions, sourceUrl);
    }

    private static string? NormalizeLookupText(string value)
    {
        var normalized = WhitespaceRegex().Replace(value.Trim(), " ").Trim('“', '”', '«', '»', '\'', '"', '.', ',', ';', ':', '!', '?', '(', ')');
        return normalized.Length is > 0 and <= 80 && normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 4
            ? normalized
            : null;
    }

    private static string CleanWikitext(string value)
    {
        var result = CommentRegex().Replace(value, "");
        result = ReferenceRegex().Replace(result, "");
        result = LinkWithLabelRegex().Replace(result, "$2");
        result = SimpleLinkRegex().Replace(result, "$1");
        for (var pass = 0; pass < 4 && result.Contains("{{", StringComparison.Ordinal); pass++)
            result = TemplateRegex().Replace(result, TemplateText);
        result = HtmlTagRegex().Replace(result, "");
        result = result.Replace("'''", "").Replace("''", "");
        return WebUtility.HtmlDecode(WhitespaceRegex().Replace(result, " ")).Trim(' ', '.', ';', ':');
    }

    private static string TemplateText(Match match)
    {
        var parts = match.Groups[1].Value.Split('|', StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return "";
        var name = parts[0].ToLowerInvariant();
        if (name is "familier" or "péjoratif" or "figuré" or "vieilli" or "rare" or "ironique" or "vulgaire")
            return $"({parts[0]}) ";
        if ((name is "lien" or "l" or "term" or "w" or "mention") && parts.Length > 1) return parts[1];
        return "";
    }

    [GeneratedRegex(@"\s+")] private static partial Regex WhitespaceRegex();
    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)] private static partial Regex CommentRegex();
    [GeneratedRegex(@"<ref\b[^>]*>.*?</ref>|<ref\b[^>]*/>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex ReferenceRegex();
    [GeneratedRegex(@"\[\[([^\]|]+)\|([^\]]+)\]\]")] private static partial Regex LinkWithLabelRegex();
    [GeneratedRegex(@"\[\[([^\]]+)\]\]")] private static partial Regex SimpleLinkRegex();
    [GeneratedRegex(@"\{\{([^{}]+)\}\}")] private static partial Regex TemplateRegex();
    [GeneratedRegex(@"<[^>]+>")] private static partial Regex HtmlTagRegex();
}
