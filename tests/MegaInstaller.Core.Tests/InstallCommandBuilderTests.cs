using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

public class InstallCommandBuilderTests
{
    [Fact]
    public void Build_ExeInstaller_RunsFileDirectlyWithItsArguments()
    {
        var entry = new InstallerEntry { FileName = "setup.exe", Arguments = "/S" };

        var (fileName, arguments) = InstallCommandBuilder.Build("/opt/installers", entry);

        Assert.Equal(Path.Combine("/opt/installers", "setup.exe"), fileName);
        Assert.Equal("/S", arguments);
    }

    [Fact]
    public void Build_MsiInstaller_WrapsWithMsiExec()
    {
        var entry = new InstallerEntry { FileName = "app.msi", Arguments = "/qn /norestart" };
        var folder = "/opt/installers";

        var (fileName, arguments) = InstallCommandBuilder.Build(folder, entry);

        Assert.Equal("msiexec.exe", fileName);
        var expectedPath = Path.Combine(folder, "app.msi");
        Assert.Equal($"/i \"{expectedPath}\" /qn /norestart", arguments);
    }

    [Fact]
    public void Build_MsiInstallerWithoutArguments_OmitsTrailingSpace()
    {
        var entry = new InstallerEntry { FileName = "app.msi", Arguments = "" };
        var folder = "/opt/installers";

        var (_, arguments) = InstallCommandBuilder.Build(folder, entry);

        Assert.Equal($"/i \"{Path.Combine(folder, "app.msi")}\"", arguments);
    }

    [Fact]
    public void Build_MsiExtensionIsCaseInsensitive()
    {
        var entry = new InstallerEntry { FileName = "APP.MSI", Arguments = "" };

        var (fileName, _) = InstallCommandBuilder.Build("/opt", entry);

        Assert.Equal("msiexec.exe", fileName);
    }
}
