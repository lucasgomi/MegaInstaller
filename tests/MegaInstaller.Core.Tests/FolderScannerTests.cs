using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

public class FolderScannerTests : IDisposable
{
    private readonly string _folder;

    public FolderScannerTests()
    {
        _folder = Directory.CreateTempSubdirectory("megainstaller-scan-").FullName;
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void FindUntrackedInstallers_ReturnsExeAndMsiNotInManifest_ButIgnoresOtherFiles()
    {
        File.WriteAllText(Path.Combine(_folder, "tracked.exe"), "");
        File.WriteAllText(Path.Combine(_folder, "new.exe"), "");
        File.WriteAllText(Path.Combine(_folder, "new.msi"), "");
        File.WriteAllText(Path.Combine(_folder, "readme.txt"), "");
        File.WriteAllText(Path.Combine(_folder, ManifestService.ManifestFileName), "{}");

        var manifest = new InstallerManifest
        {
            Items = { new InstallerEntry { FileName = "tracked.exe" } }
        };

        var untracked = FolderScanner.FindUntrackedInstallers(_folder, manifest);

        Assert.Equal(new[] { "new.exe", "new.msi" }, untracked);
    }

    [Fact]
    public void FindUntrackedInstallers_MissingFolder_ReturnsEmpty()
    {
        var result = FolderScanner.FindUntrackedInstallers(Path.Combine(_folder, "nope"), new InstallerManifest());

        Assert.Empty(result);
    }
}
