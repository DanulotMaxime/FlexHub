using System.IO;

namespace PersonalAppsHub.Services;

public static class ChangelogService
{
    public static IReadOnlyList<VersionNote> Load(string path)
    {
        if (!File.Exists(path)) return [];
        return Parse(File.ReadLines(path));
    }

    public static IReadOnlyList<VersionNote> Parse(IEnumerable<string> lines)
    {
        var result = new List<VersionNote>();
        string? version = null;
        var notes = new List<string>();

        void AddCurrent()
        {
            if (string.IsNullOrWhiteSpace(version)) return;
            var text = string.Join(Environment.NewLine, notes).Trim();
            result.Add(new VersionNote(version, string.IsNullOrWhiteSpace(text) ? "Aucune note disponible." : text));
            notes.Clear();
        }

        foreach (var line in lines)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                AddCurrent();
                version = line[3..].Trim();
            }
            else if (version != null)
            {
                notes.Add(line.StartsWith("- ", StringComparison.Ordinal) ? $"• {line[2..]}" : line);
            }
        }
        AddCurrent();
        return result;
    }
}

public sealed record VersionNote(string Version, string Notes)
{
    public override string ToString() => $"Version {Version}";
}
