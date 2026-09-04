using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

public class InstallServiceTests : IDisposable
{
    private readonly string _defaultFolder = Directory.CreateTempSubdirectory("megainstaller-installsvc-default-").FullName;
    private readonly string _overrideFolder = Directory.CreateTempSubdirectory("megainstaller-installsvc-override-").FullName;

    public void Dispose()
    {
        Directory.Delete(_defaultFolder, recursive: true);
        Directory.Delete(_overrideFolder, recursive: true);
    }

    [Fact]
    public async Task InstallOneAsync_EntryFolderOverride_ResolvesFromOverrideNotTheDefaultFolder()
    {
        // The file only exists in the override folder (standing in for a
        // web-sourced entry already downloaded into a cache folder) - not in
        // the default installers folder.
        File.WriteAllText(Path.Combine(_overrideFolder, "app.exe"), "not a real installer");

        var entry = new InstallerEntry { Id = "a", Name = "App", FileName = "app.exe" };
        var service = new InstallService(runningElevated: false);

        var withoutOverride = await service.InstallOneAsync(_defaultFolder, entry, CancellationToken.None);
        Assert.Equal(InstallOutcome.FileNotFound, withoutOverride.Outcome);

        var overrides = new Dictionary<string, string> { [entry.Id] = _overrideFolder };
        var withOverride = await service.InstallOneAsync(_defaultFolder, entry, CancellationToken.None, overrides);

        // It got past the File.Exists check this time (unlike above), proving
        // the override folder - not the default one - is what was resolved.
        // Whatever happens next (this isn't a real executable) is irrelevant here.
        Assert.NotEqual(InstallOutcome.FileNotFound, withOverride.Outcome);
    }

    [Fact]
    public async Task InstallOneAsync_EntryFolderOverride_UnrelatedEntryId_FallsBackToDefaultFolder()
    {
        File.WriteAllText(Path.Combine(_defaultFolder, "app.exe"), "not a real installer");

        var entry = new InstallerEntry { Id = "a", Name = "App", FileName = "app.exe" };
        var service = new InstallService(runningElevated: false);
        var overrides = new Dictionary<string, string> { ["some-other-id"] = _overrideFolder };

        var result = await service.InstallOneAsync(_defaultFolder, entry, CancellationToken.None, overrides);

        Assert.NotEqual(InstallOutcome.FileNotFound, result.Outcome);
    }
}
