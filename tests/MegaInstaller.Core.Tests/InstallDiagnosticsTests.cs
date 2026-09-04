using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

public class InstallDiagnosticsTests
{
    private static InstallerEntry Entry(InstallerType type = InstallerType.Msi) =>
        new() { Id = "a", Name = "App", FileName = "app.msi", Type = type };

    private static InstallResult Result(InstallOutcome outcome, int? exitCode = null) =>
        new() { EntryId = "a", Name = "App", Outcome = outcome, ExitCode = exitCode };

    [Fact]
    public void FileNotFound_ExplainsMissingFile()
    {
        var diagnosis = InstallDiagnostics.Analyze(Entry(), Result(InstallOutcome.FileNotFound));

        Assert.Contains("no se encontró", diagnosis.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.False(diagnosis.NeedsElevation);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(740)]
    [InlineData(1223)]
    public void ElevationRelatedExitCodes_FlagElevation(int exitCode)
    {
        var diagnosis = InstallDiagnostics.Analyze(Entry(), Result(InstallOutcome.Failed, exitCode));

        Assert.True(diagnosis.NeedsElevation);
    }

    [Fact]
    public void InnoSetup_ExitCode5_IsAbortedNotAccessDenied()
    {
        var entry = Entry(InstallerType.InnoSetup);

        var diagnosis = InstallDiagnostics.Analyze(entry, Result(InstallOutcome.Failed, 5));

        Assert.Contains("abortó", diagnosis.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.False(diagnosis.NeedsElevation);
    }

    [Fact]
    public void Msi1618_ExplainsConcurrentInstall()
    {
        var diagnosis = InstallDiagnostics.Analyze(Entry(), Result(InstallOutcome.Failed, 1618));

        Assert.Contains("1618", diagnosis.Summary);
        Assert.NotEmpty(diagnosis.SuggestedArguments);
    }

    [Fact]
    public void InvalidParameter_SuggestsAlternativeArguments()
    {
        var diagnosis = InstallDiagnostics.Analyze(Entry(InstallerType.Nsis), Result(InstallOutcome.Failed, 87));

        Assert.Equal(SilentArgsCatalog.GetAlternativeArguments(InstallerType.Nsis), diagnosis.SuggestedArguments);
    }

    [Fact]
    public void UnknownTypeAndUnknownCode_StillOffersTheCommonConventions()
    {
        var diagnosis = InstallDiagnostics.Analyze(Entry(InstallerType.Unknown), Result(InstallOutcome.Failed, 42));

        Assert.NotEmpty(diagnosis.SuggestedArguments);
        Assert.Contains("/S", diagnosis.SuggestedArguments);
    }
}
