using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

public class AppSettingsServiceTests : IDisposable
{
    private readonly string _path;

    public AppSettingsServiceTests()
    {
        _path = Path.Combine(Directory.CreateTempSubdirectory("megainstaller-settings-").FullName, "settings.json");
    }

    public void Dispose()
    {
        var dir = Path.GetDirectoryName(_path);
        if (dir is not null && Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaults()
    {
        var settings = new AppSettingsService(_path).Load();

        Assert.Null(settings.LastFolder);
        Assert.Null(settings.LastWindowsSessionId);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var sut = new AppSettingsService(_path);
        sut.Save(new AppSettings { LastFolder = @"C:\Installers", LastWindowsSessionId = 7 });

        var loaded = sut.Load();

        Assert.Equal(@"C:\Installers", loaded.LastFolder);
        Assert.Equal(7, loaded.LastWindowsSessionId);
    }

    [Fact]
    public void Load_WithCorruptFile_ReturnsDefaultsInsteadOfThrowing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, "not json");

        var settings = new AppSettingsService(_path).Load();

        Assert.Null(settings.LastFolder);
    }
}
