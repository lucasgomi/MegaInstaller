namespace MegaInstaller.Core.Services;

/// <summary>Converts between the comma-separated text a user types and the stored tag list.</summary>
public static class TagUtils
{
    /// <summary>Splits on commas, trims, drops empties, and removes duplicates (case-insensitive), preserving first-seen order and casing.</summary>
    public static List<string> Parse(string commaSeparatedText)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var raw in commaSeparatedText.Split(','))
        {
            var tag = raw.Trim();
            if (tag.Length == 0) continue;
            if (seen.Add(tag)) result.Add(tag);
        }

        return result;
    }

    public static string Join(IEnumerable<string> tags) => string.Join(", ", tags);

    /// <summary>Union of the existing tags with newly parsed ones, deduplicated case-insensitively.</summary>
    public static List<string> Add(IEnumerable<string> existingTags, string commaSeparatedTagsToAdd)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var tag in existingTags.Concat(Parse(commaSeparatedTagsToAdd)))
        {
            if (seen.Add(tag)) result.Add(tag);
        }

        return result;
    }

    public static bool MatchesAny(IEnumerable<string> tags, string searchText) =>
        tags.Any(tag => tag.Contains(searchText, StringComparison.OrdinalIgnoreCase));
}
