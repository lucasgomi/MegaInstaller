namespace MegaInstaller.Core.Models;

/// <summary>
/// A batch handed from a normal MegaInstaller window to an elevated copy of
/// itself, so the whole batch costs one UAC prompt instead of one per
/// installer. Written to a file in the user's own profile and deleted by the
/// elevated instance as soon as it has been read.
/// </summary>
public sealed class PendingInstallPlan
{
    public string Folder { get; set; } = string.Empty;

    public bool StopOnError { get; set; }

    public List<InstallerEntry> Entries { get; set; } = new();
}
