using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

/// <summary>Covers the newer families and the per-type flag helpers.</summary>
public class SilentArgsCatalogExtrasTests
{
    [Theory]
    [InlineData(InstallerType.WixBurn, "/quiet /norestart")]
    [InlineData(InstallerType.Squirrel, "--silent")]
    [InlineData(InstallerType.SevenZipSfx, "-y")]
    [InlineData(InstallerType.Wise, "/s")]
    public void GetSuggestedArguments_CoversNewFamilies(InstallerType type, string expected)
    {
        Assert.Equal(expected, SilentArgsCatalog.GetSuggestedArguments(type));
    }

    [Fact]
    public void AppendInstallDir_WixBurnForwardsAnMsiProperty()
    {
        var result = SilentArgsCatalog.AppendInstallDir("/quiet", InstallerType.WixBurn, @"C:\Apps\Thing");

        Assert.Equal("/quiet INSTALLFOLDER=\"C:\\Apps\\Thing\"", result);
    }

    [Fact]
    public void AppendInstallDir_SevenZipSfxUsesExtractionFlag()
    {
        var result = SilentArgsCatalog.AppendInstallDir("-y", InstallerType.SevenZipSfx, @"C:\Tools");

        Assert.Equal("-y -o\"C:\\Tools\"", result);
    }

    [Fact]
    public void SupportsInstallDir_IsFalseForFamiliesWithoutAReliableFlag()
    {
        Assert.False(SilentArgsCatalog.SupportsInstallDir(InstallerType.InstallShield));
        Assert.False(SilentArgsCatalog.SupportsInstallDir(InstallerType.Unknown));
        Assert.True(SilentArgsCatalog.SupportsInstallDir(InstallerType.Msi));
    }

    [Fact]
    public void AppendNoRestart_AddsFlagOnceOnly()
    {
        var once = SilentArgsCatalog.AppendNoRestart("/qn", InstallerType.Msi);
        var twice = SilentArgsCatalog.AppendNoRestart(once, InstallerType.Msi);

        Assert.Equal("/qn /norestart", once);
        Assert.Equal(once, twice);
    }

    [Fact]
    public void AppendNoRestart_LeavesFamiliesWithoutSuchAFlagAlone()
    {
        Assert.Equal("/S", SilentArgsCatalog.AppendNoRestart("/S", InstallerType.Nsis));
    }

    [Theory]
    [InlineData(InstallerType.Msi, "/l*v")]
    [InlineData(InstallerType.InnoSetup, "/LOG=")]
    [InlineData(InstallerType.WixBurn, "/log")]
    public void AppendLogging_UsesThePerFamilyFlag(InstallerType type, string expectedFragment)
    {
        var result = SilentArgsCatalog.AppendLogging(string.Empty, type, @"C:\log.txt");

        Assert.Contains(expectedFragment, result);
        Assert.Contains(@"C:\log.txt", result);
    }

    [Fact]
    public void AppendLogging_IgnoresFamiliesThatCannotLog()
    {
        Assert.Equal("/S", SilentArgsCatalog.AppendLogging("/S", InstallerType.Nsis, @"C:\log.txt"));
        Assert.False(SilentArgsCatalog.SupportsLogging(InstallerType.Nsis));
    }

    [Fact]
    public void GetAlternativeArguments_StartsWithTheDefaultSuggestion()
    {
        foreach (var type in new[] { InstallerType.Msi, InstallerType.Nsis, InstallerType.InnoSetup, InstallerType.WixBurn })
        {
            var alternatives = SilentArgsCatalog.GetAlternativeArguments(type);

            Assert.NotEmpty(alternatives);
            Assert.Equal(SilentArgsCatalog.GetSuggestedArguments(type), alternatives[0]);
        }
    }
}
