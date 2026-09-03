namespace MegaInstaller.Core.Models;

/// <summary>
/// One program/installer tracked by the hub. Instances live inside an
/// <see cref="InstallerManifest"/> that is persisted as megainstaller.json
/// next to the installer files.
/// </summary>
public sealed class InstallerEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    /// <summary>File name relative to the installers folder.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>URL the file was downloaded from, if added that way. Optional.</summary>
    public string? SourceUrl { get; set; }

    public InstallerType Type { get; set; } = InstallerType.Unknown;

    /// <summary>
    /// The literal argument string passed to the installer process (or to
    /// msiexec, for .msi files). What you see here is exactly what gets
    /// executed - nothing is silently added at install time.
    /// </summary>
    public string Arguments { get; set; } = string.Empty;

    /// <summary>
    /// Install directory recorded for reference/UI purposes. It only takes
    /// effect if the user (or "Insert into arguments") added the
    /// corresponding flag to <see cref="Arguments"/>.
    /// </summary>
    public string? TargetInstallDir { get; set; }

    public bool RunAsAdmin { get; set; }

    /// <summary>Whether this entry is included in bulk "Install all" runs.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Lower runs first in a batch install.</summary>
    public int Order { get; set; }

    public string Notes { get; set; } = string.Empty;

    public DateTime AddedUtc { get; set; } = DateTime.UtcNow;
}
