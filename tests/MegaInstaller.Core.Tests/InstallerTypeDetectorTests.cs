using System.Text;
using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

public class InstallerTypeDetectorTests : IDisposable
{
    private readonly string _folder;

    public InstallerTypeDetectorTests()
    {
        _folder = Directory.CreateTempSubdirectory("megainstaller-detect-").FullName;
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Detect_MsiExtension_ReturnsMsiWithoutReadingFile()
    {
        var path = Path.Combine(_folder, "missing.msi");

        Assert.Equal(InstallerType.Msi, InstallerTypeDetector.Detect(path));
    }

    [Fact]
    public void Detect_OleCompoundFileHeader_ReturnsMsiEvenWithExeExtension()
    {
        var path = WriteFile("renamed.exe", new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0 });

        Assert.Equal(InstallerType.Msi, InstallerTypeDetector.Detect(path));
    }

    [Theory]
    [InlineData("padding padding Nullsoft Install System padding", InstallerType.Nsis)]
    [InlineData("padding padding Inno Setup Setup Data padding", InstallerType.InnoSetup)]
    [InlineData("padding padding InstallShield Wizard padding", InstallerType.InstallShield)]
    public void Detect_KnownMarkerInExe_ReturnsMatchingType(string content, InstallerType expected)
    {
        var path = WriteFile("setup.exe", Encoding.ASCII.GetBytes(content));

        Assert.Equal(expected, InstallerTypeDetector.Detect(path));
    }

    [Fact]
    public void Detect_NoKnownMarker_ReturnsUnknown()
    {
        var path = WriteFile("setup.exe", Encoding.ASCII.GetBytes("just some random binary-ish content"));

        Assert.Equal(InstallerType.Unknown, InstallerTypeDetector.Detect(path));
    }

    [Fact]
    public void Detect_MarkerOnlyNearEndOfLargeFile_IsStillFound()
    {
        // Regression guard for the bounded head+tail sampling: a marker
        // placed well past the head sample, near the tail, must still hit.
        var padding = new byte[6 * 1024 * 1024];
        var marker = Encoding.ASCII.GetBytes("Nullsoft Install System");
        var content = new byte[padding.Length + marker.Length];
        Buffer.BlockCopy(padding, 0, content, 0, padding.Length);
        Buffer.BlockCopy(marker, 0, content, padding.Length, marker.Length);

        var path = WriteFile("large.exe", content);

        Assert.Equal(InstallerType.Nsis, InstallerTypeDetector.Detect(path));
    }

    [Fact]
    public void Detect_MissingFile_ReturnsUnknown()
    {
        var path = Path.Combine(_folder, "does-not-exist.exe");

        Assert.Equal(InstallerType.Unknown, InstallerTypeDetector.Detect(path));
    }

    private string WriteFile(string name, byte[] content)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, content);
        return path;
    }
}
