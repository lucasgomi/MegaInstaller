namespace MegaInstaller.Core.Models;

/// <summary>
/// Installer technology behind a program, used to pick sensible default
/// silent-install switches and install-directory flags. "Unknown" means no
/// silent flags are assumed and the installer is simply launched as-is.
/// </summary>
public enum InstallerType
{
    Unknown,
    Msi,
    Nsis,
    InnoSetup,
    InstallShield,
    Custom
}
