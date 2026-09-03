using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

public class SilentArgsCatalogTests
{
    [Theory]
    [InlineData(InstallerType.Msi, "/qn /norestart")]
    [InlineData(InstallerType.Nsis, "/S")]
    [InlineData(InstallerType.InnoSetup, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-")]
    [InlineData(InstallerType.InstallShield, "/s /v\"/qn /norestart\"")]
    [InlineData(InstallerType.Unknown, "")]
    public void GetSuggestedArguments_ReturnsKnownDefaultsPerType(InstallerType type, string expected)
    {
        Assert.Equal(expected, SilentArgsCatalog.GetSuggestedArguments(type));
    }

    [Fact]
    public void AppendInstallDir_Msi_AddsQuotedInstallDirProperty()
    {
        var result = SilentArgsCatalog.AppendInstallDir("/qn /norestart", InstallerType.Msi, @"C:\Apps\Foo");

        Assert.Equal("/qn /norestart INSTALLDIR=\"C:\\Apps\\Foo\"", result);
    }

    [Fact]
    public void AppendInstallDir_InnoSetup_AddsQuotedDirFlag()
    {
        var result = SilentArgsCatalog.AppendInstallDir("", InstallerType.InnoSetup, @"C:\Apps\Foo");

        Assert.Equal("/DIR=\"C:\\Apps\\Foo\"", result);
    }

    [Fact]
    public void AppendInstallDir_Nsis_AddsUnquotedDirFlagLast()
    {
        var result = SilentArgsCatalog.AppendInstallDir("/S", InstallerType.Nsis, @"C:\Apps\Foo Bar");

        Assert.Equal(@"/S /D=C:\Apps\Foo Bar", result);
    }

    [Fact]
    public void AppendInstallDir_InstallShield_LeavesArgumentsUnchanged()
    {
        var result = SilentArgsCatalog.AppendInstallDir("/s", InstallerType.InstallShield, @"C:\Apps\Foo");

        Assert.Equal("/s", result);
    }

    [Fact]
    public void AppendInstallDir_BlankDir_LeavesArgumentsUnchanged()
    {
        var result = SilentArgsCatalog.AppendInstallDir("/S", InstallerType.Nsis, "   ");

        Assert.Equal("/S", result);
    }
}
