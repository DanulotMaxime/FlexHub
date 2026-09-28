using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PersonalAppsHub.Services;

public sealed record ToneAnalysisResult(string PrimaryTone, string ConfidenceText, string Summary,
    IReadOnlyList<string> Clues);

/// <summary>
/// Analyse linguistique locale et explicable. Elle ne prétend pas remplacer un modèle de langage :
/// chaque conclusion est reliée à des indices visibles et les scores faibles restent prudents.
/// </summary>
public sealed class ToneAnalysisService
{
    private sealed record Marker(string Text, double Weight);
    private sealed record ToneDefinition(string Name, string Description, Marker[] Phrases, Marker[] Words);

    private static Marker M(string text, double weight) => new(text, weight);

    private static readonly ToneDefinition[] Definitions =
    [
        new("Critique", "Le message formule un reproche ou remet en cause une décision, un comportement ou une situation.",
        [
            M("vous ne faites rien", 4), M("sans prendre en compte", 3), M("n ecoute pas", 3),
            M("n ecoutent pas", 3), M("bouchons d oreille", 3.5), M("jamais d avoir tort", 4),
            M("n en ont rien a foutre", 4), M("n en a rien a foutre", 4), M("cela dit tout de vous", 3),
            M("voila ce que ca donne", 2.5), M("voila ce que sa donne", 2.5), M("tour d ivoire", 4),
            M("encourage le spam", 3.5), M("trop facile a obtenir", 2), M("trop faciles a obtenir", 2),
            M("ne peut pas jouer", 2), M("ne peuvent pas jouer", 2), M("aucun effort", 3),
            M("toujours la meme chose", 2.5), M("vous avez deja eu", 2), M("reglez ce", 2.5),
            M("si vous ne", 1.5), M("pourquoi mettre ca ici", 1.5), M("le probleme", 1.2),
            M("ce probleme", 1.2), M("joue 2 fois", 2), M("jouent 2 fois", 2),
            M("quitte le serv", 2.5), M("quittent le serv", 2.5), M("leur vision", 1.5)
        ],
        [
            M("injust*", 2), M("desequilibr*", 2.2), M("equilibrage", 1.2), M("reproch*", 1.8),
            M("critiqu*", 1.5), M("inadmissible", 3), M("inacceptable", 3), M("abus*", 1.6),
            M("ignor*", 1.5), M("favoritisme", 2.5), M("partial*", 2), M("responsable*", 1)
        ]),

        new("Agressif", "Le message contient des attaques, des insultes ou des formulations hostiles visant une personne ou un groupe.",
        [
            M("ferme la", 4), M("ta gueule", 5), M("rammer la gueule", 4), M("spammer vous la gueule", 4),
            M("je te deteste", 4), M("ne sait rien faire d autre", 3), M("ne savent rien faire d autre", 3),
            M("allez vous faire", 4), M("va te faire", 4), M("foutez vous", 3.5), M("n importe quoi", 1.5)
        ],
        [
            M("idiot*", 3), M("debile*", 3), M("stupide*", 3), M("incompetent*", 3),
            M("incapable*", 2.5), M("minable*", 3), M("ridicule*", 2.2), M("connard*", 4),
            M("abruti*", 4), M("merde*", 2), M("insulte*", 1.5), M("haine*", 2)
        ]),

        new("Frustré", "Le message exprime une accumulation d’agacement, une saturation ou un problème qui persiste.",
        [
            M("j en ai marre", 4), M("j en ai eu ma claque", 4), M("ras le bol", 4),
            M("ca m enerve", 4), M("encore une fois", 2.5), M("toujours pas", 2.5),
            M("ne marche pas", 2.5), M("ne fonctionne pas", 2.5), M("jusqu a saturation", 2.5),
            M("a outrance", 2.5), M("c est penible", 3), M("c est agacant", 3),
            M("combien de fois", 2), M("on en revient toujours", 2.5)
        ],
        [
            M("frustr*", 2.5), M("ener*", 2.5), M("agac*", 2.5), M("satur*", 2),
            M("marre", 2.5), M("penible*", 2), M("spam*", 1.3), M("bug*", 1.2),
            M("probleme*", .8), M("impossible", 1.2), M("lass*", 2), M("fatigu*", 1.5)
        ]),

        new("Résigné", "Le message annonce un retrait, un abandon ou une rupture après une déception.",
        [
            M("je me casse", 4), M("j arrete", 4), M("j abandonne", 4), M("je m en vais", 4),
            M("je ne reviendrai", 4), M("ne rejouerai", 3.5), M("ne rejouerais", 3.5),
            M("ne pas perdre grand chose", 3.5), M("pas perdu grand chose", 3.5),
            M("faites comme vous voulez", 3), M("reglez le ou ne le reglez pas", 3),
            M("peut etre a un jour", 2.5), M("bonne continuation", 1.5), M("le depart de", 2),
            M("ne s amusent plus", 2.5), M("tant pis", 2.5), M("sera mort", 2.5)
        ],
        [
            M("abandon*", 2.5), M("depart*", 1.5), M("quitte*", 1.8), M("partir*", 1.5),
            M("renonce*", 2.5), M("fini", 1), M("adieu", 2.5)
        ]),

        new("Défensif", "Le message justifie une position, relativise un reproche ou protège un groupe ou une décision.",
        [
            M("pas mechant mais", 4), M("sans vouloir etre mechant", 3.5), M("je comprends mais", 3),
            M("il faut comprendre", 2.5), M("de leur point de vue", 2.5), M("les gens se plaignent", 2.5),
            M("tout le monde se plaint", 2.5), M("jouent comme ils veulent", 2), M("on ne peut pas", 1.5),
            M("ce n est pas leur faute", 3), M("pour leur defense", 3), M("je dis ca sans", 2.5),
            M("ne le prends pas mal", 3), M("ne le prenez pas mal", 3)
        ],
        [
            M("justifi*", 1.8), M("defend*", 1.8), M("relativis*", 1.5), M("pourtant", .7),
            M("cependant", .7), M("mais", .4)
        ]),

        new("Inquiet", "Le message exprime une crainte, une préoccupation ou un besoin d’être rassuré.",
        [
            M("je suis inquiet", 4), M("j ai peur", 4), M("ca m inquiete", 4), M("je crains", 3),
            M("est ce normal", 2), M("que va t il se passer", 2.5), M("j espere que", 1.5),
            M("rassurez moi", 3), M("rassure moi", 3)
        ],
        [
            M("inquiet*", 3), M("angoiss*", 3.5), M("peur*", 2.5), M("crain*", 2.2),
            M("danger*", 2), M("risque*", 1.2), M("souci*", 1.2), M("preoccup*", 2.5)
        ]),

        new("Triste", "Le message exprime de la tristesse, de la déception ou une perte.",
        [
            M("je suis triste", 4), M("ca me fait mal", 4), M("coeur brise", 4),
            M("je suis decu", 3.5), M("ca me decoit", 3.5), M("tu me manques", 3),
            M("vous me manquez", 3), M("quelle tristesse", 3)
        ],
        [
            M("triste*", 3), M("decu*", 2.8), M("malheureu*", 2.5), M("regret*", 2),
            M("dommage", 1.3), M("pleur*", 2.5), M("chagrin*", 3), M("deception*", 2.5)
        ]),

        new("Enthousiaste", "Le message exprime une émotion positive forte, de l’admiration ou de l’impatience positive.",
        [
            M("j adore", 3.5), M("trop bien", 3), M("j ai hate", 3), M("hate de", 2.5),
            M("c est genial", 3), M("c est incroyable", 3), M("quel bonheur", 3.5),
            M("super nouvelle", 3), M("tres bonne nouvelle", 3)
        ],
        [
            M("genial*", 2.8), M("excellent*", 2.8), M("incroyable*", 2.5), M("magnifique*", 2.8),
            M("super", 1.8), M("parfait*", 2), M("heureu*", 2.5), M("ravie*", 2.5),
            M("felicitation*", 2.5), M("formidable*", 2.8)
        ]),

        new("Chaleureux", "Le message adopte une attitude positive, reconnaissante ou bienveillante.",
        [
            M("avec plaisir", 3), M("prends soin de toi", 3), M("prenez soin de vous", 3),
            M("merci beaucoup", 2.5), M("bonne journee", 2), M("bonne soiree", 2),
            M("je te remercie", 2), M("je vous remercie", 2), M("content de te", 2),
            M("content de vous", 2), M("bienvenue parmi", 2.5)
        ],
        [
            M("merci", 1), M("bienvenue", 1.5), M("amicalement", 2), M("gentil*", 1.5),
            M("sympa*", 1.5), M("bisou*", 2), M("affectueusement", 2.5)
        ]),

        new("Urgent", "Le message insiste sur une action rapide, une échéance proche ou une priorité élevée.",
        [
            M("de toute urgence", 4), M("au plus vite", 3.5), M("des que possible", 2.5),
            M("sans attendre", 3), M("avant ce soir", 2), M("avant demain", 2),
            M("c est urgent", 3.5), M("pas une minute a perdre", 3.5)
        ],
        [
            M("urgent*", 3), M("immediat*", 3), M("rapidement", 1.5), M("vite", 1),
            M("asap", 3), M("prioritaire*", 2.2), M("deadline*", 1.8)
        ]),

        new("Professionnel", "Le message utilise un registre formel, structuré ou administratif.",
        [
            M("je vous prie", 2.5), M("bien cordialement", 3), M("suite a notre", 2),
            M("nous vous informons", 2.5), M("veuillez trouver", 2.5), M("je vous remercie de", 2),
            M("dans l attente de", 2.5), M("nous restons a votre disposition", 3)
        ],
        [
            M("cordialement", 2.5), M("veuillez", 2), M("madame", 1.3), M("monsieur", 1.3),
            M("concernant", .8), M("objet", .5), M("respectueusement", 2.5)
        ]),

        new("Incertain", "Le message hésite, formule une hypothèse ou manque d’assurance.",
        [
            M("je ne sais pas", 3), M("je ne suis pas sur", 3), M("je ne suis pas certain", 3),
            M("peut etre", 2), M("je suppose", 2), M("on verra", 2), M("comme tu veux", 1.5),
            M("j hesite", 3), M("difficile a dire", 2.5), M("sans doute", 1)
        ],
        [
            M("eventuellement", 1.5), M("probablement", 1), M("possiblement", 1),
            M("incertain*", 2.5), M("hesit*", 2.5), M("bof", 2)
        ]),

        new("Directif", "Le message donne des instructions, impose une action ou fixe une limite.",
        [
            M("vous devez", 3), M("tu dois", 3), M("il faut que", 2), M("faites le", 2.5),
            M("fais le", 2.5), M("je veux que", 2.5), M("merci de", 1.5), M("arretez de", 3),
            M("arrete de", 3), M("reglez le", 2.5)
        ],
        [
            M("obligatoire*", 2), M("interdit*", 2), M("exige*", 2.5), M("ordonne*", 3)
        ])
    ];

    public ToneAnalysisResult Analyze(string text)
    {
        var value = text?.Trim() ?? string.Empty;
        if (value.Length < 3) throw new ArgumentException("Saisissez un message à analyser.");

        var normalized = Normalize(value);
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var scores = Definitions.ToDictionary(definition => definition.Name, _ => 0d);
        var evidence = Definitions.ToDictionary(definition => definition.Name, _ => new List<(string Text, double Weight)>());

        foreach (var definition in Definitions)
        {
            foreach (var marker in definition.Phrases)
            {
                var occurrences = Math.Min(3, Occurrences(normalized, marker.Text));
                if (occurrences == 0) continue;
                scores[definition.Name] += marker.Weight * occurrences;
                evidence[definition.Name].Add((marker.Text, marker.Weight * occurrences));
            }

            foreach (var marker in definition.Words)
            {
                var matches = Math.Min(4, tokens.Count(token => MatchesWord(token, marker.Text)));
                if (matches == 0) continue;
                scores[definition.Name] += marker.Weight * matches;
                evidence[definition.Name].Add((marker.Text.TrimEnd('*'), marker.Weight * matches));
            }
        }

        ApplyContextRules(value, normalized, tokens, scores, evidence);

        var ranked = scores.OrderByDescending(item => item.Value).ToArray();
        var best = ranked[0];
        if (best.Value < 1.25)
            return new("Neutre", "prudente", "Ton principal : Neutre · aucun signal suffisamment net.",
            ["Le texte paraît surtout factuel, ou les indices sont trop faibles pour attribuer un ton sans extrapoler."]);

        var nuances = ranked.Skip(1)
            .Where(item => item.Value >= 2 && item.Value >= best.Value * .38)
            .Take(2)
            .ToArray();
        var margin = best.Value - ranked[1].Value;
        var confidence = best.Value >= 7 && margin >= 1.5 ? "élevée" : best.Value >= 3 ? "moyenne" : "prudente";
        var intensity = DetermineIntensity(best.Value, value);
        var displayedTone = best.Key + (nuances.Length > 0
            ? $" · nuance {string.Join(" et ", nuances.Select(item => item.Key.ToLowerInvariant()))}"
            : string.Empty);

        var clues = new List<string>
        {
            Definitions.First(item => item.Name == best.Key).Description,
            $"Intensité estimée : {intensity}."
        };
        foreach (var nuance in nuances)
            clues.Add($"Nuance {nuance.Key.ToLowerInvariant()} : {Definitions.First(item => item.Name == nuance.Key).Description.ToLowerInvariant()}");

        var detected = new[] { best.Key }.Concat(nuances.Select(item => item.Key))
            .SelectMany(tone => evidence[tone])
            .OrderByDescending(item => item.Weight)
            .Select(item => item.Text)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(7)
            .ToArray();
        if (detected.Length > 0)
            clues.Add($"Indices repérés : « {string.Join(" », « ", detected)} ».");

        if (value.Length < 20)
            clues.Add("Le texte est très court : le résultat doit être interprété avec prudence.");
        if (normalized.Contains("pas mechant mais", StringComparison.Ordinal))
            clues.Add("L’introduction cherche à adoucir un désaccord qui devient ensuite explicite.");

        return new(displayedTone, confidence,
            $"Ton principal : {best.Key} · confiance {confidence} · intensité {intensity}." +
            (nuances.Length > 0 ? $" Nuance : {string.Join(", ", nuances.Select(item => item.Key.ToLowerInvariant()))}." : string.Empty), clues);
    }

    private static void ApplyContextRules(string original, string normalized, string[] tokens,
        IDictionary<string, double> scores, IDictionary<string, List<(string Text, double Weight)>> evidence)
    {
        var letters = original.Count(char.IsLetter);
        var uppercaseRatio = letters == 0 ? 0 : (double)original.Count(char.IsUpper) / letters;
        var exclamations = original.Count(character => character == '!');
        var questions = original.Count(character => character == '?');
        var emojis = original.EnumerateRunes().Count(rune => IsEmoji(rune.Value));
        var secondPerson = tokens.Count(token => token is "tu" or "toi" or "te" or "vous" or "votre" or "vos");
        var negativeScore = scores["Critique"] + scores["Agressif"] + scores["Frustré"] + scores["Résigné"];
        var positiveScore = scores["Enthousiaste"] + scores["Chaleureux"];

        if (uppercaseRatio >= .55 && letters >= 8)
            AddContext("Agressif", 1.2, "majuscules insistantes", scores, evidence);

        if (exclamations >= 2)
        {
            if (negativeScore > positiveScore && negativeScore > 0)
            {
                var target = scores["Agressif"] > scores["Frustré"] ? "Agressif" : "Frustré";
                AddContext(target, Math.Min(1.5, exclamations * .3), "ponctuation insistante", scores, evidence);
            }
            else if (positiveScore > 0)
                AddContext(scores["Enthousiaste"] >= scores["Chaleureux"] ? "Enthousiaste" : "Chaleureux",
                    Math.Min(1.2, exclamations * .25), "ponctuation enthousiaste", scores, evidence);
        }

        if (questions >= 2)
        {
            var target = secondPerson > 0 && negativeScore > 0 ? "Critique" : "Incertain";
            AddContext(target, Math.Min(1.5, questions * .35), "questions répétées", scores, evidence);
        }

        if (secondPerson >= 2 && negativeScore >= 2)
            AddContext("Critique", Math.Min(1.5, secondPerson * .2), "reproche adressé directement", scores, evidence);

        if (normalized.Contains("pas mechant mais", StringComparison.Ordinal) ||
            normalized.Contains("sans vouloir etre mechant", StringComparison.Ordinal))
            AddContext("Critique", 1.5, "désaccord atténué", scores, evidence);

        if (Regex.IsMatch(normalized, @"\b(mais|pourtant|cependant|alors que)\b") && negativeScore > 0)
            AddContext("Défensif", .8, "opposition argumentative", scores, evidence);

        if (emojis > 0 && positiveScore > 0)
            AddContext("Chaleureux", Math.Min(1.5, emojis * .5), "émoji positif", scores, evidence);

        // Une négation proche d'un qualificatif positif évite les contresens du type « pas génial ».
        if (Regex.IsMatch(normalized, @"\b(ne|n|pas|jamais|plus)\s+(?:\w+\s+){0,2}(genial|excellent|super|parfait|heureux)\b"))
        {
            scores["Enthousiaste"] = Math.Max(0, scores["Enthousiaste"] - 3);
            AddContext("Critique", 1.5, "appréciation positive niée", scores, evidence);
        }
    }

    private static void AddContext(string tone, double weight, string clue,
        IDictionary<string, double> scores, IDictionary<string, List<(string Text, double Weight)>> evidence)
    {
        scores[tone] += weight;
        evidence[tone].Add((clue, weight));
    }

    private static string DetermineIntensity(double score, string text)
    {
        var emphasis = text.Count(character => character is '!' or '?');
        if (score >= 10 || score >= 7 && emphasis >= 3) return "forte";
        if (score >= 4) return "modérée";
        return "faible";
    }

    private static bool MatchesWord(string token, string pattern) => pattern.EndsWith('*')
        ? token.StartsWith(pattern[..^1], StringComparison.Ordinal)
        : token.Equals(pattern, StringComparison.Ordinal);

    private static int Occurrences(string text, string term)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(term, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += term.Length;
        }
        return count;
    }

    private static bool IsEmoji(int codePoint) =>
        codePoint is >= 0x1F300 and <= 0x1FAFF or >= 0x2600 and <= 0x26FF or >= 0x2700 and <= 0x27BF;

    private static string Normalize(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }
        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }
}
