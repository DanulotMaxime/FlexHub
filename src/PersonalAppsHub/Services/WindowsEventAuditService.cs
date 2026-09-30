using System.Diagnostics.Eventing.Reader;
using System.Text.RegularExpressions;

namespace PersonalAppsHub.Services;

public sealed record WindowsEventAuditEntry(
    string Severity,
    string Category,
    string Source,
    int EventId,
    int Count,
    DateTime LastOccurrence,
    string Summary,
    string Explanation,
    string Recommendation)
{
    public string EventCode => $"{Source} · {EventId}";
    public string CountText => Count == 1 ? "1 fois" : $"{Count} fois";
    public string LastOccurrenceText => LastOccurrence.ToString("dd/MM HH:mm");
}

public sealed record WindowsEventAuditResult(
    IReadOnlyList<WindowsEventAuditEntry> Entries,
    int EventsRead,
    int InaccessibleLogs,
    DateTime Since);

public sealed record WindowsEventGuidance(string Category, string Explanation, string Recommendation);

public sealed class WindowsEventAuditService
{
    private const int MaximumEventsPerLog = 600;

    public Task<WindowsEventAuditResult> ScanAsync(int days = 7, CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(days, cancellationToken), cancellationToken);

    private static WindowsEventAuditResult Scan(int days, CancellationToken cancellationToken)
    {
        var since = DateTime.Now.AddDays(-Math.Clamp(days, 1, 30));
        var events = new List<RawEvent>();
        var inaccessibleLogs = 0;
        foreach (var logName in new[] { "System", "Application" })
        {
            try { ReadLog(logName, since, events, cancellationToken); }
            catch (EventLogException ex)
            {
                inaccessibleLogs++;
                AppLog.Write($"Journal Windows {logName} inaccessible : {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                inaccessibleLogs++;
                AppLog.Write($"Journal Windows {logName} refusé : {ex.Message}");
            }
        }

        var grouped = events
            .GroupBy(item => $"{item.LogName}\0{item.Provider}\0{item.EventId}\0{Normalize(item.Message)}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildEntry(group.OrderByDescending(item => item.TimeCreated).ToArray()))
            .OrderBy(entry => SeverityRank(entry.Severity))
            .ThenByDescending(entry => entry.Count)
            .ThenByDescending(entry => entry.LastOccurrence)
            .Take(100)
            .ToArray();
        return new WindowsEventAuditResult(grouped, events.Count, inaccessibleLogs, since);
    }

    private static void ReadLog(string logName, DateTime since, List<RawEvent> target, CancellationToken cancellationToken)
    {
        var milliseconds = Math.Max(1, (long)(DateTime.Now - since).TotalMilliseconds);
        var query = new EventLogQuery(logName, PathType.LogName,
            $"*[System[(Level=1 or Level=2) and TimeCreated[timediff(@SystemTime) &lt;= {milliseconds}]]]")
        { ReverseDirection = true, TolerateQueryErrors = true };
        using var reader = new EventLogReader(query);
        for (var count = 0; count < MaximumEventsPerLog; count++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var record = reader.ReadEvent();
            if (record is null) break;
            var message = SafeMessage(record);
            target.Add(new RawEvent(logName, record.ProviderName ?? "Source inconnue", record.Id,
                record.Level ?? 2, record.TimeCreated ?? DateTime.Now, message));
        }
    }

    private static WindowsEventAuditEntry BuildEntry(IReadOnlyList<RawEvent> group)
    {
        var latest = group[0];
        var guidance = Explain(latest.Provider, latest.EventId, latest.Message);
        var severity = latest.Level == 1 ? "Critique" : IsHighPriority(latest.Provider, latest.EventId) ? "Important" : "Erreur";
        return new WindowsEventAuditEntry(severity, guidance.Category, latest.Provider, latest.EventId, group.Count,
            latest.TimeCreated, Shorten(latest.Message, 240), guidance.Explanation, guidance.Recommendation);
    }

    public static WindowsEventGuidance Explain(string provider, int id, string message)
    {
        var text = $"{provider} {message}";
        if (provider.Contains("Kernel-Power", StringComparison.OrdinalIgnoreCase) && id == 41 || id == 6008)
            return new("Arrêt brutal", "Windows n’a pas été arrêté normalement. Cela peut venir d’une coupure, d’un blocage, d’une surchauffe ou d’une instabilité matérielle.", "Corréler l’heure avec les températures, tests de charge et erreurs WHEA. Ne pas conclure à l’alimentation sur ce seul événement.");
        if (provider.Contains("WHEA", StringComparison.OrdinalIgnoreCase))
            return new("Matériel", "Windows a reçu une erreur matérielle signalée par le processeur ou le bus PCIe.", "Vérifier BIOS, réglages d’overclocking/undervolt, mémoire et pilotes chipset. Des occurrences répétées justifient un test matériel.");
        if (provider.Contains("BugCheck", StringComparison.OrdinalIgnoreCase) ||
            provider.Contains("WER-SystemErrorReporting", StringComparison.OrdinalIgnoreCase) && id == 1001)
            return new("Écran bleu", "Windows a redémarré après une erreur système fatale et a normalement créé un fichier de diagnostic.", "Noter le code d’arrêt affiché dans le résumé, conserver le fichier minidump et rechercher les erreurs pilote ou WHEA survenues juste avant.");
        if (ContainsAny(text, "disk", "stornvme", "storahci", "iaStor", "Ntfs", "volmgr") || id is 7 or 51 or 55 or 129 or 153)
            return new("Stockage", "Le stockage, son pilote ou le système de fichiers a rencontré une erreur ou un délai anormal.", "Sauvegarder les données importantes, consulter Santé du stockage et vérifier câbles, firmware et pilote avant toute réparation.");
        if (provider.Contains("volsnap", StringComparison.OrdinalIgnoreCase))
            return new("Sauvegarde / restauration", "Windows n’a pas pu créer ou conserver une copie instantanée utilisée par la sauvegarde et la restauration.", "Vérifier l’espace libre et le service Cliché instantané des volumes. Ne supprimer les clichés existants qu’après avoir vérifié vos sauvegardes.");
        if (id == 4101 || ContainsAny(text, "nvlddmkm", "amdkmdag", "display driver"))
            return new("Pilote graphique", "Le pilote graphique s’est arrêté ou a été réinitialisé par Windows.", "Comparer l’heure avec les freezes en jeu, vérifier températures et stabilité, puis envisager une installation propre du pilote officiel.");
        if (provider.Contains("Kernel-PnP", StringComparison.OrdinalIgnoreCase) && id == 219)
            return new("Pilote de périphérique", "Un pilote de périphérique n’a pas pu être chargé au démarrage. L’appareil peut néanmoins fonctionner avec un autre pilote.", "Identifier le périphérique cité, puis vérifier son état dans le Gestionnaire de périphériques et les mises à jour de pilotes applicables.");
        if (provider.Contains("DistributedCOM", StringComparison.OrdinalIgnoreCase) && id == 10016)
            return new("Autorisation Windows", "Une application n’a pas obtenu une autorisation DCOM. Cet événement est très courant et reste souvent sans conséquence visible.", "Ignorer s’il n’accompagne aucun symptôme. Éviter de modifier les autorisations du registre ou DCOM uniquement pour faire disparaître cet événement.");
        if (provider.Contains("Application Hang", StringComparison.OrdinalIgnoreCase) || id == 1002)
            return new("Application bloquée", "Une application a cessé de répondre et Windows l’a considérée comme bloquée.", "Identifier l’application dans le résumé, vérifier sa charge CPU/RAM et ses mises à jour, puis corréler avec les erreurs survenues à la même heure.");
        if (provider.Contains(".NET Runtime", StringComparison.OrdinalIgnoreCase) && id == 1026)
            return new("Application .NET", "Une application .NET s’est arrêtée à cause d’une exception non gérée.", "Relever le nom de l’application et le type d’exception dans le résumé, puis mettre à jour ou réparer l’application concernée.");
        if (id == 1000 || provider.Contains("Application Error", StringComparison.OrdinalIgnoreCase))
            return new("Application", "Une application s’est arrêtée de façon inattendue.", "Identifier le programme et le module fautif dans le résumé, puis vérifier sa mise à jour et ses fichiers avant de le réinstaller.");
        if (id == 1001 || provider.Contains("Windows Error Reporting", StringComparison.OrdinalIgnoreCase))
            return new("Rapport de plantage", "Windows a enregistré un rapport après un plantage ou un blocage.", "Chercher un événement Application Error proche à la même heure pour obtenir le programme et le module responsables.");
        if (id is 7031 or 7034 || provider.Contains("Service Control Manager", StringComparison.OrdinalIgnoreCase))
            return new("Service Windows", "Un service Windows ou tiers s’est arrêté de manière inattendue.", "Si l’erreur se répète, identifier le service cité et vérifier son logiciel ou son pilote associé.");
        if (id == 1014 || provider.Contains("DNS", StringComparison.OrdinalIgnoreCase))
            return new("Réseau / DNS", "Une résolution de nom a expiré ou échoué.", "Comparer avec le monitoring réseau ; vérifier la connexion et le serveur DNS si les occurrences sont fréquentes.");
        if (provider.Contains("Schannel", StringComparison.OrdinalIgnoreCase))
            return new("Connexion sécurisée", "Une négociation TLS chiffrée a échoué entre une application et un service distant.", "Vérifier la date et l’heure du PC, puis identifier l’application ou le serveur concerné. Une occurrence isolée peut simplement venir d’un service distant incompatible.");
        if (ContainsAny(provider, "Time-Service", "TimeService", "W32Time"))
            return new("Synchronisation de l’heure", "Windows n’a pas réussi à synchroniser l’horloge avec sa source de temps.", "Vérifier la connexion Internet, le fuseau horaire et la synchronisation automatique de l’heure si le décalage persiste.");
        if (ContainsAny(provider, "TPM", "SecureBoot"))
            return new("Sécurité matérielle", "Le TPM ou le démarrage sécurisé a signalé une erreur de configuration, de certificat ou de communication avec le firmware.", "Installer les mises à jour Windows et du BIOS/UEFI proposées par le fabricant. Ne pas effacer le TPM sans sauvegarder auparavant la clé de récupération BitLocker.");
        return new(latestCategory(provider), "Windows a enregistré une erreur provenant de cette source. Le message résumé donne le contexte disponible.", "Surveiller sa répétition et corréler l’heure avec le symptôme observé avant toute modification.");
    }

    private static string latestCategory(string provider) =>
        provider.Contains("Security", StringComparison.OrdinalIgnoreCase) ? "Sécurité" : "Windows / logiciel";

    private static bool IsHighPriority(string provider, int id) =>
        provider.Contains("WHEA", StringComparison.OrdinalIgnoreCase) ||
        provider.Contains("Kernel-Power", StringComparison.OrdinalIgnoreCase) || id is 7 or 41 or 51 or 55 or 129 or 153 or 4101;

    private static string SafeMessage(EventRecord record)
    {
        try { return record.FormatDescription()?.Trim() ?? $"Événement {record.Id} sans description."; }
        catch (EventLogException) { return $"Événement {record.Id} : description Windows indisponible."; }
    }

    private static string Normalize(string message) => Regex.Replace(message, @"\b(?:0x)?[0-9a-f]{6,}\b|\b\d{4,}\b", "#",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
    private static string Shorten(string value, int maximum) => value.Length <= maximum ? value : value[..maximum].TrimEnd() + "…";
    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    private static int SeverityRank(string severity) => severity == "Critique" ? 0 : severity == "Important" ? 1 : 2;
    private sealed record RawEvent(string LogName, string Provider, int EventId, byte Level, DateTime TimeCreated, string Message);
}
