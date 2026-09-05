using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

public class ExportPackageServiceTests : IDisposable
{
    private readonly string _sourceFolder = Directory.CreateTempSubdirectory("megainstaller-export-src-").FullName;
    private readonly string _destFolder = Directory.CreateTempSubdirectory("megainstaller-export-dst-").FullName;
    private readonly string _zipPath;

    public ExportPackageServiceTests()
    {
        _zipPath = Path.Combine(Directory.CreateTempSubdirectory("megainstaller-export-zip-").FullName, "pack.zip");
    }

    public void Dispose()
    {
        Directory.Delete(_sourceFolder, recursive: true);
        Directory.Delete(_destFolder, recursive: true);
        var zipDir = Path.GetDirectoryName(_zipPath)!;
        if (Directory.Exists(zipDir))
        {
            Directory.Delete(zipDir, recursive: true);
        }
    }

    private (InstallerManifest Manifest, InstanceDefinition Instance) BuildSourceFixture(bool includeWebEntry = false)
    {
        Directory.CreateDirectory(_sourceFolder);
        Directory.CreateDirectory(Path.Combine(_sourceFolder, CustomIconPaths.FolderName));

        var localEntry = new InstallerEntry { Id = "local-1", Name = "Local App", FileName = "setup.exe", Order = 10 };
        File.WriteAllText(Path.Combine(_sourceFolder, localEntry.FileName), "fake installer bytes");

        var items = new List<InstallerEntry> { localEntry };
        if (includeWebEntry)
        {
            items.Add(new InstallerEntry { Id = "web-1", Name = "Web App", FileName = "web.exe", MirrorUrl = "https://example.com/web.exe", Order = 20 });
        }

        File.WriteAllBytes(Path.Combine(_sourceFolder, CustomIconPaths.FolderName, "icon.png"), new byte[] { 1, 2, 3 });

        var instance = new InstanceDefinition
        {
            Id = "instance-1",
            Name = "Mi Pack",
            Description = "Descripción",
            IconKey = CustomIconPaths.BuildKey("icon.png"),
            InstallerIds = items.Select(i => i.Id).ToList(),
        };

        var manifest = new InstallerManifest { Items = items, Instances = { instance } };
        new ManifestService().Save(_sourceFolder, manifest);

        return (manifest, instance);
    }

    [Fact]
    public void ExportInstance_ThenImport_RestoresInstallerFileManifestAndIcon()
    {
        var (manifest, instance) = BuildSourceFixture();

        ExportPackageService.ExportInstance(_sourceFolder, manifest, instance, _zipPath);
        ExportPackageService.Import(_zipPath, _destFolder);

        var imported = new ManifestService().Load(_destFolder);
        var importedInstance = Assert.Single(imported.Instances);
        Assert.Equal("Mi Pack", importedInstance.Name);
        Assert.Equal(CustomIconPaths.BuildKey("icon.png"), importedInstance.IconKey);

        var importedItem = Assert.Single(imported.Items);
        Assert.Equal("setup.exe", importedItem.FileName);
        Assert.Equal("fake installer bytes", File.ReadAllText(Path.Combine(_destFolder, "setup.exe")));
        Assert.True(File.Exists(Path.Combine(_destFolder, CustomIconPaths.FolderName, "icon.png")));
    }

    [Fact]
    public void Import_WebSourcedEntry_CarriesNoFileButKeepsManifestEntry()
    {
        var (manifest, instance) = BuildSourceFixture(includeWebEntry: true);

        ExportPackageService.ExportInstance(_sourceFolder, manifest, instance, _zipPath);
        ExportPackageService.Import(_zipPath, _destFolder);

        var imported = new ManifestService().Load(_destFolder);
        var webItem = Assert.Single(imported.Items, i => i.MirrorUrl is not null);
        Assert.Equal("https://example.com/web.exe", webItem.MirrorUrl);
        Assert.False(File.Exists(Path.Combine(_destFolder, "web.exe")));
    }

    [Fact]
    public void Import_SamePackageTwice_IsIdempotent()
    {
        var (manifest, instance) = BuildSourceFixture();
        ExportPackageService.ExportInstance(_sourceFolder, manifest, instance, _zipPath);

        ExportPackageService.Import(_zipPath, _destFolder);
        ExportPackageService.Import(_zipPath, _destFolder);

        var imported = new ManifestService().Load(_destFolder);
        Assert.Single(imported.Instances);
        Assert.Single(imported.Items);
    }

    [Fact]
    public void Import_FilenameCollisionWithUnrelatedFile_RenamesAndNeverOverwrites()
    {
        var (manifest, instance) = BuildSourceFixture();
        ExportPackageService.ExportInstance(_sourceFolder, manifest, instance, _zipPath);

        // A different, pre-existing "setup.exe" already lives in the
        // destination folder - unrelated to the one being imported.
        Directory.CreateDirectory(_destFolder);
        File.WriteAllText(Path.Combine(_destFolder, "setup.exe"), "someone else's file");

        ExportPackageService.Import(_zipPath, _destFolder);

        Assert.Equal("someone else's file", File.ReadAllText(Path.Combine(_destFolder, "setup.exe")));
        var renamedPath = Path.Combine(_destFolder, "setup (2).exe");
        Assert.True(File.Exists(renamedPath));
        Assert.Equal("fake installer bytes", File.ReadAllText(renamedPath));

        var imported = new ManifestService().Load(_destFolder);
        var importedItem = Assert.Single(imported.Items);
        Assert.Equal("setup (2).exe", importedItem.FileName);
    }

    [Fact]
    public void PreviewImport_ReportsNewAndSkippedWithoutTouchingDisk()
    {
        var (manifest, instance) = BuildSourceFixture();
        ExportPackageService.ExportInstance(_sourceFolder, manifest, instance, _zipPath);

        var firstPreview = ExportPackageService.PreviewImport(_zipPath, _destFolder);
        Assert.Single(firstPreview.NewInstances);
        Assert.Empty(firstPreview.SkippedInstances);
        Assert.Single(firstPreview.NewInstallers);
        Assert.Empty(firstPreview.SkippedInstallers);
        Assert.Empty(firstPreview.RenamedFiles);
        Assert.False(File.Exists(Path.Combine(_destFolder, "setup.exe")));

        ExportPackageService.Import(_zipPath, _destFolder);

        var secondPreview = ExportPackageService.PreviewImport(_zipPath, _destFolder);
        Assert.Empty(secondPreview.NewInstances);
        Assert.Single(secondPreview.SkippedInstances);
        Assert.Empty(secondPreview.NewInstallers);
        Assert.Single(secondPreview.SkippedInstallers);
    }

    [Fact]
    public void ExportAll_IncludesEveryInstanceAndInstaller()
    {
        var (manifest, _) = BuildSourceFixture();
        var secondInstance = new InstanceDefinition { Id = "instance-2", Name = "Otro pack", InstallerIds = new List<string>() };
        manifest.Instances.Add(secondInstance);
        new ManifestService().Save(_sourceFolder, manifest);

        ExportPackageService.ExportAll(_sourceFolder, manifest, _zipPath);
        ExportPackageService.Import(_zipPath, _destFolder);

        var imported = new ManifestService().Load(_destFolder);
        Assert.Equal(2, imported.Instances.Count);
        Assert.Single(imported.Items);
    }

    [Fact]
    public void Import_NotAMegaInstallerPackage_ThrowsInvalidDataException()
    {
        var badZipDir = Directory.CreateTempSubdirectory("megainstaller-badzip-").FullName;
        try
        {
            var badZipPath = Path.Combine(badZipDir, "not-a-package.zip");
            using (var archive = System.IO.Compression.ZipFile.Open(badZipPath, System.IO.Compression.ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("readme.txt");
                using var stream = entry.Open();
                using var writer = new StreamWriter(stream);
                writer.Write("just a random zip");
            }

            Assert.Throws<InvalidDataException>(() => ExportPackageService.PreviewImport(badZipPath, _destFolder));
            Assert.Throws<InvalidDataException>(() => ExportPackageService.Import(badZipPath, _destFolder));
        }
        finally
        {
            Directory.Delete(badZipDir, recursive: true);
        }
    }
}
