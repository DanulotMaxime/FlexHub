using System.Diagnostics;
using System.Text.Json;

namespace PersonalAppsHub.Services;

public sealed class XmpMonitorService
{
    public async Task<XmpCheckResult> CheckAsync(int expectedMinimum, int expectedMaximum)
    {
        var command = "$ErrorActionPreference='Stop'; @(Get-CimInstance Win32_PhysicalMemory | Select-Object PartNumber,ConfiguredClockSpeed) | ConvertTo-Json -Compress";
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -Command \"{command}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Impossible de démarrer la vérification de la mémoire.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        await process.WaitForExitAsync(timeout.Token);
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "Windows ne permet pas de lire la fréquence de la mémoire." : error.Trim());

        using var document = JsonDocument.Parse(output);
        var modules = new List<XmpMemoryModule>();
        var root = document.RootElement;
        IEnumerable<JsonElement> items = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().ToArray() : new[] { root };
        foreach (var item in items)
        {
            var part = item.TryGetProperty("PartNumber", out var partNode) ? partNode.GetString()?.Trim() ?? "RAM" : "RAM";
            var speed = item.TryGetProperty("ConfiguredClockSpeed", out var speedNode) && speedNode.TryGetInt32(out var value) ? value : 0;
            modules.Add(new XmpMemoryModule(part, speed));
        }
        return Evaluate(modules, expectedMinimum, expectedMaximum);
    }

    public static XmpCheckResult Evaluate(IReadOnlyList<XmpMemoryModule> modules, int expectedMinimum, int expectedMaximum)
    {
        if (expectedMinimum <= 0 || expectedMaximum < expectedMinimum)
            throw new ArgumentOutOfRangeException(nameof(expectedMinimum), "La plage mémoire est invalide.");
        if (modules.Count == 0 || modules.Any(module => module.ConfiguredSpeed <= 0))
            return new(false, false, 0, modules, "La fréquence mémoire n’est pas publiée par Windows.");
        var minimumSpeed = modules.Min(module => module.ConfiguredSpeed);
        var active = minimumSpeed >= expectedMinimum;
        var expectedDescription = expectedMinimum == expectedMaximum
            ? $"au moins {expectedMinimum} MT/s"
            : $"la plage {expectedMinimum}–{expectedMaximum} MT/s";
        return new(true, active, minimumSpeed, modules, active
            ? minimumSpeed > expectedMaximum && expectedMinimum != expectedMaximum
                ? $"Fréquence détectée : {minimumSpeed} MT/s, au-dessus de {expectedDescription}. XMP est probablement actif."
                : $"Fréquence conforme : {minimumSpeed} MT/s ({expectedDescription}). XMP est probablement actif."
            : $"Fréquence insuffisante : {minimumSpeed} MT/s ; valeur attendue : {expectedDescription}. Vérifiez XMP dans le BIOS.");
    }
}

public sealed record XmpMemoryModule(string PartNumber, int ConfiguredSpeed);
public sealed record XmpCheckResult(bool IsAvailable, bool IsProbablyActive, int MinimumSpeed, IReadOnlyList<XmpMemoryModule> Modules, string Message);
