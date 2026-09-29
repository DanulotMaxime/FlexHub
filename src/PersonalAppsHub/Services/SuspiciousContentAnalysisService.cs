using System.Net;
using System.IO;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace PersonalAppsHub.Services;

public sealed record SuspiciousContentAnalysis(
    string TargetType,
    string Verdict,
    int RiskScore,
    string Summary,
    IReadOnlyList<string> Findings,
    string TechnicalDetails);

public sealed partial class SuspiciousContentAnalysisService
{
    private static readonly HashSet<string> RiskyExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".exe", ".msi", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".scr", ".com", ".lnk", ".hta" };
    private static readonly HashSet<string> LinkShorteners = new(StringComparer.OrdinalIgnoreCase)
        { "bit.ly", "tinyurl.com", "t.co", "cutt.ly", "is.gd", "rb.gy", "shorturl.at", "rebrand.ly" };
    private static readonly Dictionary<string, string[]> TrustedBrandDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        ["paypal"] = ["paypal.com"], ["microsoft"] = ["microsoft.com", "live.com", "office.com"],
        ["google"] = ["google.com", "google.fr"], ["apple"] = ["apple.com", "icloud.com"],
        ["amazon"] = ["amazon.com", "amazon.fr"], ["discord"] = ["discord.com", "discord.gg"],
        ["steam"] = ["steampowered.com", "steamcommunity.com"], ["netflix"] = ["netflix.com"]
    };

    public SuspiciousContentAnalysis AnalyzeUrl(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("Saisissez un lien à analyser.");
        var original = WebUtility.HtmlDecode(input.Trim());
        var score = 0;
        var findings = new List<string>();
        if (original.Any(character => char.IsControl(character) && character is not '\r' and not '\n' and not '\t') || HiddenUnicode().IsMatch(original))
            Add(35, "Le texte contient des caractères invisibles ou de contrôle pouvant masquer le lien.", ref score, findings);
        var value = ExtractUrl(original, ref score, findings);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Saisissez une adresse complète commençant par http:// ou https://.");

        if (uri.Scheme == "http") Add(15, "Connexion non chiffrée (HTTP) : les données et la destination peuvent être altérées.", ref score, findings);
        if (IPAddress.TryParse(uri.Host, out var address))
        {
            Add(25, "Le lien utilise une adresse IP au lieu d’un nom de domaine.", ref score, findings);
            if (IsPrivateAddress(address)) Add(10, "L’adresse vise un réseau local ou réservé, inhabituel dans un message externe.", ref score, findings);
        }
        if (uri.Host.Contains("xn--", StringComparison.OrdinalIgnoreCase)) Add(15, "Nom de domaine internationalisé (Punycode) : vérifiez visuellement les caractères.", ref score, findings);
        if (!string.IsNullOrEmpty(uri.UserInfo)) Add(40, "Le lien place du texte avant @ pour détourner l’attention du véritable domaine.", ref score, findings);
        if (!uri.IsDefaultPort) Add(10, $"Port réseau non standard ({uri.Port}).", ref score, findings);
        if (original.Contains('\\')) Add(20, "Le lien contient une barre oblique inversée, interprétée différemment selon les logiciels.", ref score, findings);
        if (uri.Host.Count(character => character == '.') >= 4) Add(10, "Nombre inhabituel de sous-domaines.", ref score, findings);
        var labels = uri.Host.Split('.');
        if (labels.Take(Math.Max(0, labels.Length - 2)).Any(IsRandomLookingLabel))
            Add(15, "Sous-domaine long et aléatoire, fréquent dans les liens jetables ou de suivi.", ref score, findings);
        if (value.Length > 180) Add(10, "Lien anormalement long.", ref score, findings);
        if (score >= 10 && SuspiciousUrlWords().IsMatch(value)) Add(10, "Le lien combine d’autres anomalies avec des termes fréquemment utilisés dans l’hameçonnage.", ref score, findings);
        if (RedirectPath().IsMatch(uri.AbsolutePath)) Add(15, "Chemin ressemblant à un lien de clic ou de redirection.", ref score, findings);
        if (LongHexToken().IsMatch(uri.AbsolutePath)) Add(15, "Identifiant hexadécimal long pouvant servir au suivi ou à masquer la destination.", ref score, findings);
        if (EncodedCharacters().Matches(value).Count >= 4) Add(10, "Le lien contient beaucoup de caractères encodés.", ref score, findings);
        if (uri.Host.Split('.').Any(part => part.Length > 35)) Add(10, "Une partie du domaine est anormalement longue.", ref score, findings);
        if (LinkShorteners.Contains(uri.Host.TrimStart("www.".ToCharArray()))) Add(10, "Raccourcisseur de lien : la destination finale est masquée.", ref score, findings);
        if (RiskyDownload().IsMatch(uri.AbsolutePath)) Add(30, "Le lien pointe vers un type de fichier capable d’exécuter du code.", ref score, findings);
        if (NestedUrlParameter().IsMatch(uri.Query)) Add(10, "Le lien contient une autre adresse comme destination ou redirection.", ref score, findings);
        AddBrandImpersonationFinding(uri, ref score, findings);
        if (findings.Count == 0) findings.Add("Aucun indice évident détecté par l’analyse locale.");

        var unicodeHost = TryGetUnicodeHost(uri.Host);
        return Build("Lien", score, findings,
            $"Domaine réel : {uri.Host}\nDomaine affiché : {unicodeHost}\nProtocole : {uri.Scheme.ToUpperInvariant()}\nPort : {(uri.IsDefaultPort ? "standard" : uri.Port)}\nAdresse normalisée : {uri.AbsoluteUri}");
    }

    public SuspiciousContentAnalysis AnalyzeFile(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Le fichier sélectionné est introuvable.", path);
        var file = new FileInfo(path);
        var score = 0;
        var findings = new List<string>();
        var extension = file.Extension;
        var executable = RiskyExtensions.Contains(extension);
        if (executable) Add(10, $"Type de fichier capable d’exécuter du code ({extension}).", ref score, findings);
        if (DoubleExtension().IsMatch(file.Name)) Add(30, "Double extension susceptible de masquer le type réel du fichier.", ref score, findings);
        if (SuspiciousFileWords().IsMatch(file.Name)) Add(15, "Le nom emploie un terme souvent utilisé pour inciter à ouvrir un fichier.", ref score, findings);

        var signedBy = "Non applicable";
        if (executable && extension is ".exe" or ".msi" or ".dll")
        {
            try
            {
                using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
                signedBy = certificate.GetNameInfo(X509NameType.SimpleName, false);
                findings.Add($"Signature numérique présente : {signedBy} (identité uniquement, confiance non garantie). ");
            }
            catch (CryptographicException)
            {
                signedBy = "Aucune signature détectée";
                Add(25, "Aucune signature numérique détectée.", ref score, findings);
            }
        }

        var zone = ReadInternetZone(path);
        if (zone is not null) Add(10, "Windows indique que le fichier provient d’Internet.", ref score, findings);
        if (findings.Count == 0) findings.Add("Aucun indice évident détecté par l’analyse locale.");
        using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(SHA256.HashData(stream));
        return Build("Fichier", score, findings,
            $"Nom : {file.Name}\nTaille : {FormatSize(file.Length)}\nSHA-256 : {hash}\nSignature : {signedBy}\nZone Internet : {zone ?? "non détectée"}");
    }

    private static SuspiciousContentAnalysis Build(string type, int score, IReadOnlyList<string> findings, string details)
    {
        score = Math.Min(score, 100);
        var verdict = score switch { >= 50 => "Risque élevé", >= 25 => "Lien suspect", >= 10 => "Prudence recommandée", _ => "Aucun danger évident" };
        var summary = score == 0
            ? "L’analyse locale ne révèle aucun signal évident, mais elle ne garantit jamais qu’un contenu est sûr."
            : $"{findings.Count} indice(s) de prudence détecté(s). N’ouvrez pas le contenu sans vérifier sa provenance.";
        return new(type, verdict, score, summary, findings, details);
    }

    private static void Add(int points, string finding, ref int score, ICollection<string> findings)
    {
        score += points;
        findings.Add(finding);
    }

    private static bool IsRandomLookingLabel(string label)
    {
        if (label.Length < 10) return false;
        var hasLetter = label.Any(char.IsLetter);
        var hasDigit = label.Any(char.IsDigit);
        var transitions = label.Zip(label.Skip(1), (left, right) => char.IsDigit(left) != char.IsDigit(right)).Count(changed => changed);
        return hasLetter && hasDigit && transitions >= 3;
    }

    private static string ExtractUrl(string input, ref int score, ICollection<string> findings)
    {
        var markdown = MarkdownLink().Match(input);
        if (markdown.Success)
        {
            var displayed = markdown.Groups[1].Value;
            var target = markdown.Groups[2].Value;
            if (Uri.TryCreate(displayed, UriKind.Absolute, out var shown) && Uri.TryCreate(target, UriKind.Absolute, out var actual) &&
                !shown.Host.Equals(actual.Host, StringComparison.OrdinalIgnoreCase))
                Add(50, $"Le domaine affiché ({shown.Host}) ne correspond pas à la destination réelle ({actual.Host}).", ref score, findings);
            return target;
        }

        var href = HtmlHref().Match(input);
        if (href.Success) return href.Groups[1].Value;
        var urls = PlainUrl().Matches(input).Select(match => match.Value.TrimEnd(')', ']', '}', '"', '\'')).Distinct().ToArray();
        if (urls.Length > 1) Add(20, "Le texte contient plusieurs destinations différentes.", ref score, findings);
        return urls.FirstOrDefault() ?? input.Trim();
    }

    private static void AddBrandImpersonationFinding(Uri uri, ref int score, ICollection<string> findings)
    {
        var host = uri.Host.ToLowerInvariant();
        foreach (var (brand, trustedDomains) in TrustedBrandDomains)
        {
            if (!host.Contains(brand, StringComparison.OrdinalIgnoreCase)) continue;
            if (trustedDomains.Any(domain => host.Equals(domain, StringComparison.OrdinalIgnoreCase) || host.EndsWith('.' + domain, StringComparison.OrdinalIgnoreCase))) continue;
            Add(35, $"Le domaine mentionne « {brand} » sans appartenir aux domaines officiels connus de cette marque.", ref score, findings);
        }
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return true;
        var bytes = address.GetAddressBytes();
        return address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
               (bytes[0] == 10 || bytes[0] == 127 || bytes[0] == 192 && bytes[1] == 168 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 || bytes[0] == 169 && bytes[1] == 254);
    }

    private static string TryGetUnicodeHost(string host)
    {
        try { return new IdnMapping().GetUnicode(host); }
        catch (ArgumentException) { return host; }
    }

    private static string? ReadInternetZone(string path)
    {
        try
        {
            var content = File.ReadAllText(path + ":Zone.Identifier");
            var match = Regex.Match(content, @"ZoneId=(\d+)");
            return match.Success ? match.Groups[1].Value : null;
        }
        catch { return null; }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824d:N1} Go",
        >= 1_048_576 => $"{bytes / 1_048_576d:N1} Mo",
        >= 1024 => $"{bytes / 1024d:N1} Ko",
        _ => $"{bytes} o"
    };

    [GeneratedRegex(@"(?:login|connexion|verify|verification|secure|account|compte|password|motdepasse|urgent|gift|cadeau|crypto|wallet)", RegexOptions.IgnoreCase)]
    private static partial Regex SuspiciousUrlWords();

    [GeneratedRegex(@"%(?:[0-9a-fA-F]{2})")]
    private static partial Regex EncodedCharacters();

    [GeneratedRegex("[\\u200B-\\u200F\\u202A-\\u202E\\u2060-\\u2069\\uFEFF]")]
    private static partial Regex HiddenUnicode();

    [GeneratedRegex(@"\[\s*(https?://[^\]\s]+)\s*\]\(\s*(https?://[^)\s]+)\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex MarkdownLink();

    [GeneratedRegex("href\\s*=\\s*[\"'](https?://[^\"']+)[\"']", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlHref();

    [GeneratedRegex(@"https?://[^\s<>]+", RegexOptions.IgnoreCase)]
    private static partial Regex PlainUrl();

    [GeneratedRegex(@"/(?:cl|click|redirect|redir|track|tracking)(?:/|$)", RegexOptions.IgnoreCase)]
    private static partial Regex RedirectPath();

    [GeneratedRegex(@"(?:^|/)[0-9a-fA-F]{20,}(?:/|$)")]
    private static partial Regex LongHexToken();

    [GeneratedRegex(@"\.(?:exe|msi|msix|bat|cmd|ps1|vbs|js|jse|scr|com|hta|lnk|iso|img|dll)(?:$|/)", RegexOptions.IgnoreCase)]
    private static partial Regex RiskyDownload();

    [GeneratedRegex(@"(?:[?&](?:url|uri|redirect|redir|target|dest|destination|continue|next)=)(?:https?%3A|https?://)", RegexOptions.IgnoreCase)]
    private static partial Regex NestedUrlParameter();

    [GeneratedRegex(@"\.(?:pdf|jpg|jpeg|png|docx?|xlsx?|txt)\.(?:exe|scr|com|bat|cmd|js|vbs|lnk)$", RegexOptions.IgnoreCase)]
    private static partial Regex DoubleExtension();

    [GeneratedRegex(@"(?:facture|invoice|urgent|password|mot.?de.?passe|crack|keygen|gratuit|free|document|scan)", RegexOptions.IgnoreCase)]
    private static partial Regex SuspiciousFileWords();
}
