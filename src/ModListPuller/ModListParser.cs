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
        var nameMatches = NameLine().Matches(markdown);
        for (var nameIndex = 0; nameIndex < nameMatches.Count; nameIndex++)
        {
            var nameMatch = nameMatches[nameIndex];
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

            var detailsStart = nameMatch.Index + nameMatch.Length;
            var detailsEnd = nameIndex + 1 < nameMatches.Count
                ? nameMatches[nameIndex + 1].Index
                : markdown.Length;
            var details = markdown[detailsStart..detailsEnd];
            foreach (Match link in MarkdownLink().Matches(details))
            {
                if (IsPatchLabel(CleanLabel(link.Groups[1].Value)))
                {
                    mods.Add(new ModEntry($"{parentName} [Patch]", link.Groups[2].Value));
                }
            }
        }

        return mods;
    }

    private static string CleanLabel(string label) =>
        label.Replace("**", "").Replace("__", "").Replace("\\", "").Trim();

    private static bool IsPatchLabel(string label) =>
        label.Equals("patch", StringComparison.OrdinalIgnoreCase)
        || label.EndsWith(" patch", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^\*\*Name:\*\*\s*(.+)$", RegexOptions.Multiline)]
    private static partial Regex NameLine();

    [GeneratedRegex(@"\[([^]]+)]\((https?://[^)\s]+)\)")]
    private static partial Regex MarkdownLink();
}

public sealed record ModEntry(
    string Name,
    string Url,
    ModMetadata? Metadata = null);
