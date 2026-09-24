using System.Text.RegularExpressions;

namespace ModListPuller;

public static partial class ModListParser
{
    public static IReadOnlyList<ModEntry> Parse(string markdown)
    {
        const string marker = "## Mod List";
        var markerPosition = markdown.IndexOf(marker, StringComparison.Ordinal);
        if (markerPosition == -1)
        {
            throw new ArgumentException($"Source is missing the '{marker}' heading", nameof(markdown));
        }

        var mods = new List<ModEntry>();
        foreach (Match nameMatch in NameLine().Matches(markdown[markerPosition..]))
        {
            var nameField = nameMatch.Groups[1].Value;
            var links = MarkdownLink().Matches(nameField);
            if (links.Count == 0)
            {
                throw new ArgumentException($"Name field has no web link: {nameField}", nameof(markdown));
            }

            var parentName = CleanLabel(links[0].Groups[1].Value);
            for (var index = 0; index < links.Count; index++)
            {
                var linkName = CleanLabel(links[index].Groups[1].Value);
                var name = index switch
                {
                    0 => parentName,
                    _ when linkName.Equals("patch", StringComparison.OrdinalIgnoreCase) => $"{parentName} [Patch]",
                    _ => $"{parentName} - {linkName}"
                };

                mods.Add(new ModEntry(name, links[index].Groups[2].Value));
            }
        }

        return mods;
    }

    private static string CleanLabel(string label) =>
        label.Replace("**", "").Replace("__", "").Replace("\\", "").Trim();

    [GeneratedRegex(@"^\*\*Name:\*\*\s*(.+)$", RegexOptions.Multiline)]
    private static partial Regex NameLine();

    [GeneratedRegex(@"\[([^]]+)]\((https?://[^)\s]+)\)")]
    private static partial Regex MarkdownLink();
}

public sealed record ModEntry(
    string Name,
    string Url,
    ModMetadata? Metadata = null);
