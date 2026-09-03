using MegaInstaller.Core.Exceptions;
using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

public class ManifestServiceTests : IDisposable
{
    private readonly string _folder;
    private readonly ManifestService _sut = new();

    public ManifestServiceTests()
    {
        _folder = Directory.CreateTempSubdirectory("megainstaller-tests-").FullName;
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Load_WhenNoFileExists_ReturnsEmptyManifest()
    {
        var manifest = _sut.Load(_folder);

        Assert.Equal(1, manifest.Version);
        Assert.Empty(manifest.Items);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        var manifest = new InstallerManifest
        {
            Items =
            {
                new InstallerEntry
                {
                    Name = "7-Zip",
                    FileName = "7z-x64.exe",
                    SourceUrl = "https://example.com/7z-x64.exe",
                    Type = InstallerType.Nsis,
                    Arguments = "/S",
                    TargetInstallDir = @"C:\Apps\7-Zip",
                    RunAsAdmin = true,
                    Enabled = true,
                    Order = 5,
                    Notes = "instalado desde el hub"
                }
            }
        };

        _sut.Save(_folder, manifest);
        var loaded = _sut.Load(_folder);

        var entry = Assert.Single(loaded.Items);
        Assert.Equal("7-Zip", entry.Name);
        Assert.Equal("7z-x64.exe", entry.FileName);
        Assert.Equal("https://example.com/7z-x64.exe", entry.SourceUrl);
        Assert.Equal(InstallerType.Nsis, entry.Type);
        Assert.Equal("/S", entry.Arguments);
        Assert.Equal(@"C:\Apps\7-Zip", entry.TargetInstallDir);
        Assert.True(entry.RunAsAdmin);
        Assert.Equal(5, entry.Order);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsInstances()
    {
        var manifest = new InstallerManifest
        {
            Items = { new InstallerEntry { Id = "a", Name = "App A" } },
            Instances =
            {
                new InstanceDefinition
                {
                    Name = "Pack básico",
                    Description = "Lo esencial",
                    InstallerIds = { "a" },
                    Order = 1,
                }
            }
        };

        _sut.Save(_folder, manifest);
        var loaded = _sut.Load(_folder);

        var instance = Assert.Single(loaded.Instances);
        Assert.Equal("Pack básico", instance.Name);
        Assert.Equal(new[] { "a" }, instance.InstallerIds);
    }

    [Fact]
    public void Save_WritesHumanReadableEnumNames()
    {
        var manifest = new InstallerManifest
        {
            Items = { new InstallerEntry { Name = "Test", FileName = "test.msi", Type = InstallerType.Msi } }
        };

        _sut.Save(_folder, manifest);
        var json = File.ReadAllText(_sut.GetManifestPath(_folder));

        Assert.Contains("\"Msi\"", json);
    }

    [Fact]
    public void Load_WithCorruptJson_ThrowsManifestException()
    {
        File.WriteAllText(_sut.GetManifestPath(_folder), "{ this is not valid json");

        Assert.Throws<ManifestException>(() => _sut.Load(_folder));
    }
}
