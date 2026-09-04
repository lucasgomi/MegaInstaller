using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

public class Sha256ServiceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("megainstaller-sha256-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task ComputeAsync_EmptyFile_MatchesKnownVector()
    {
        var path = Path.Combine(_dir, "empty.bin");
        File.WriteAllBytes(path, Array.Empty<byte>());

        var hash = await Sha256Service.ComputeAsync(path, CancellationToken.None);

        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", hash);
    }

    [Fact]
    public async Task ComputeAsync_KnownContent_MatchesKnownVector()
    {
        var path = Path.Combine(_dir, "hello.txt");
        File.WriteAllText(path, "hello world");

        var hash = await Sha256Service.ComputeAsync(path, CancellationToken.None);

        Assert.Equal("b94d27b9934d3e08a52e52d7da7dabfac484efe37a5380ee9088f7ace2efcde9", hash);
    }

    [Fact]
    public async Task ComputeAsync_ReturnsLowercaseHex()
    {
        var path = Path.Combine(_dir, "x.bin");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 255 });

        var hash = await Sha256Service.ComputeAsync(path, CancellationToken.None);

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, hash.ToLowerInvariant());
    }
}
