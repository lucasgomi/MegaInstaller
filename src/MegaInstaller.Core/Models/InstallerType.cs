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
    Custom,

    /// <summary>WiX "Burn" bootstrapper bundle (an .exe wrapping one or more MSIs).</summary>
    WixBurn,

    /// <summary>Squirrel.Windows setup (Electron/.NET desktop apps: Discord, VS Code-style updaters).</summary>
    Squirrel,

    /// <summary>Self-extracting 7-Zip archive.</summary>
    SevenZipSfx,

    /// <summary>App package (.msix/.appx/.msixbundle) installed through Add-AppxPackage.</summary>
    Msix,

    /// <summary>Windows Update standalone package (.msu) installed through wusa.exe.</summary>
    Msu,

    /// <summary>Wise Installation System package.</summary>
    Wise,
}
