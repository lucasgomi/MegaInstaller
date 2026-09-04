namespace MegaInstaller.Core.Models;

/// <summary>
/// A named "pack": a set of installers (by reference, via <see cref="InstallerEntry.Id"/>)
/// that get installed together. Lets one installers folder serve several
/// different bundles without duplicating any files - an installer can
/// belong to any number of instances at once.
/// </summary>
public sealed class InstanceDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Key into the app's bundled icon pack (e.g. "controller"), or null for no icon.</summary>
    public string? IconKey { get; set; }

    /// <summary>"#RRGGBB" accent color for this instance's card, or null to use the theme's default accent.</summary>
    public string? ColorHex { get; set; }

    /// <summary>References to <see cref="InstallerEntry.Id"/>. Stale ids (installer removed) are ignored when resolving.</summary>
    public List<string> InstallerIds { get; set; } = new();

    public int Order { get; set; }

    public DateTime AddedUtc { get; set; } = DateTime.UtcNow;
}
