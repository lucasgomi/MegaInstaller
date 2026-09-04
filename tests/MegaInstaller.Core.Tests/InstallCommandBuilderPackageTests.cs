using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

/// <summary>The package formats that are installed through a Windows host tool rather than run directly.</summary>
public class InstallCommandBuilderPackageTests
{
    private const string Folder = "/installers";

    [Fact]
    public void Msix_GoesThroughAddAppxPackage()
    {
        var entry = new InstallerEntry { Name = "App", FileName = "app.msix", Type = InstallerType.Msix };

        var (fileName, arguments) = InstallCommandBuilder.Build(Folder, entry);

        Assert.Equal("powershell.exe", fileName);
        Assert.Contains("Add-AppxPackage", arguments);
        Assert.Contains(Path.Combine(Folder, "app.msix"), arguments);
    }

    [Fact]
    public void Msix_EscapesSingleQuotesInThePath()
    {
        var entry = new InstallerEntry { Name = "App", FileName = "it's.msix", Type = InstallerType.Msix };

        var (_, arguments) = InstallCommandBuilder.Build(Folder, entry);

        Assert.Contains("it''s.msix", arguments);
    }

    [Fact]
    public void Msu_GoesThroughWusaAndIsQuietByDefault()
    {
        var entry = new InstallerEntry { Name = "Update", FileName = "patch.msu", Type = InstallerType.Msu };

        var (fileName, arguments) = InstallCommandBuilder.Build(Folder, entry);

        Assert.Equal("wusa.exe", fileName);
        Assert.Contains("/quiet", arguments);
        Assert.Contains("/norestart", arguments);
    }

    [Fact]
    public void Msu_KeepsExplicitArgumentsInsteadOfTheDefault()
    {
        var entry = new InstallerEntry { Name = "Update", FileName = "patch.msu", Arguments = "/passive" };

        var (_, arguments) = InstallCommandBuilder.Build(Folder, entry);

        Assert.Contains("/passive", arguments);
        Assert.DoesNotContain("/quiet", arguments);
    }

    [Fact]
    public void PlainExe_IsStillLaunchedDirectly()
    {
        var entry = new InstallerEntry { Name = "App", FileName = "setup.exe", Arguments = "/S" };

        var (fileName, arguments) = InstallCommandBuilder.Build(Folder, entry);

        Assert.Equal(Path.Combine(Folder, "setup.exe"), fileName);
        Assert.Equal("/S", arguments);
    }
}
