using System.Text.RegularExpressions;

namespace MegaInstaller.App;

/// <summary>
/// Light cleanup of a GitHub release body for display in a plain text box -
/// not a real Markdown renderer, just strips the syntax that would
/// otherwise show up as literal punctuation (##, **, [text](url), `code`).
/// </summary>
public static partial class MarkdownLite
{
    public static string ToPlainText(string markdown)
    {
        var text = HeaderMarker().Replace(markdown, "");
        text = Link().Replace(text, "$1");
        text = Bold().Replace(text, "$1");
        text = InlineCode().Replace(text, "$1");
        return text.Trim();
    }

    [GeneratedRegex(@"^#{1,6}\s*", RegexOptions.Multiline)]
    private static partial Regex HeaderMarker();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"\*\*([^*]+)\*\*")]
    private static partial Regex Bold();

    [GeneratedRegex(@"`([^`]+)`")]
    private static partial Regex InlineCode();
}
