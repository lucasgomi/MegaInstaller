namespace MegaInstaller.Core.Services;

/// <summary>
/// What build this is and where to check for a newer one. Releases are
/// tagged "v1", "v2", ... (see .github/workflows/release.yml) - plain
/// sequential integers, not semver - so <see cref="CurrentVersion"/> is
/// bumped by hand in the same commit that ships each tag, and comparison
/// is numeric rather than a version-string compare.
/// </summary>
public static class ReleaseInfo
{
    public const string CurrentVersion = "v22";

    public const string RepoOwner = "lucasgomi";

    public const string RepoName = "MegaInstaller";

    /// <summary>True when <paramref name="latestTag"/> (e.g. "v17") is a numerically newer release than <see cref="CurrentVersion"/>.</summary>
    public static bool IsNewer(string? latestTag) => IsNewer(CurrentVersion, latestTag);

    /// <summary>
    /// Same comparison, with the "current" side passed in explicitly rather
    /// than always reading the live <see cref="CurrentVersion"/> constant -
    /// what makes this testable against arbitrary version pairs.
    /// </summary>
    public static bool IsNewer(string currentVersion, string? latestTag)
    {
        var current = ParseVersionNumber(currentVersion);
        var latest = ParseVersionNumber(latestTag);
        return current.HasValue && latest.HasValue && latest.Value > current.Value;
    }

    private static int? ParseVersionNumber(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var trimmed = tag.TrimStart('v', 'V');
        return int.TryParse(trimmed, out var number) ? number : null;
    }
}
