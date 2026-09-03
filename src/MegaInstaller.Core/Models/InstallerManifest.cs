namespace MegaInstaller.Core.Models;

/// <summary>
/// The "information file" (megainstaller.json) stored inside an installers
/// folder. It is what lets the hub know how to run each installer
/// automatically - flags, target directory, elevation - without the app
/// having to guess every time.
/// </summary>
public sealed class InstallerManifest
{
    public int Version { get; set; } = 1;

    public List<InstallerEntry> Items { get; set; } = new();
}
