using System.Net;
using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

public class WebInstallerCacheServiceTests : IDisposable
{
    private readonly string _cacheDir = Directory.CreateTempSubdirectory("megainstaller-webcache-").FullName;

    public void Dispose() => Directory.Delete(_cacheDir, recursive: true);

    private static InstallerEntry MakeEntry(string url, string fileName = "app.exe", string? expectedSha256 = null) => new()
    {
        Id = "entry-1",
        Name = "App",
        FileName = fileName,
        MirrorUrl = url,
        ExpectedSha256 = expectedSha256,
    };

    private static WebInstallerCacheService MakeService(HttpMessageHandler handler) =>
        new(new DownloadService(new HttpClient(handler)));

    [Fact]
    public async Task DownloadAsync_Success_WritesFileAndReturnsPath()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[] { 1, 2, 3, 4 }),
        });
        using var service = MakeService(handler);
        var entry = MakeEntry("https://example.com/app.exe");

        var result = await service.DownloadAsync(entry, _cacheDir, null, CancellationToken.None);

        Assert.Equal(WebDownloadOutcome.Success, result.Outcome);
        Assert.True(File.Exists(result.LocalPath));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(result.LocalPath!));
    }

    [Fact]
    public async Task DownloadAsync_HttpError_ReturnsFailedWithoutThrowing()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var service = MakeService(handler);
        var entry = MakeEntry("https://example.com/missing.exe");

        var result = await service.DownloadAsync(entry, _cacheDir, null, CancellationToken.None);

        Assert.Equal(WebDownloadOutcome.Failed, result.Outcome);
        Assert.NotNull(result.ErrorMessage);
        Assert.False(File.Exists(Path.Combine(_cacheDir, "app.exe")));
    }

    [Fact]
    public async Task DownloadAsync_EmptyBody_ReturnsFailedAndCleansUp()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Array.Empty<byte>()),
        });
        using var service = MakeService(handler);
        var entry = MakeEntry("https://example.com/empty.exe");

        var result = await service.DownloadAsync(entry, _cacheDir, null, CancellationToken.None);

        Assert.Equal(WebDownloadOutcome.Failed, result.Outcome);
        Assert.False(File.Exists(Path.Combine(_cacheDir, "app.exe")));
    }

    [Fact]
    public async Task DownloadAsync_HashMismatch_ReturnsHashMismatchWithActualHash()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 9, 9, 9 }) });
        using var service = MakeService(handler);
        var entry = MakeEntry("https://example.com/app.exe", expectedSha256: new string('0', 64));

        var result = await service.DownloadAsync(entry, _cacheDir, null, CancellationToken.None);

        Assert.Equal(WebDownloadOutcome.HashMismatch, result.Outcome);
        Assert.NotNull(result.ActualSha256);
        Assert.NotEqual(entry.ExpectedSha256, result.ActualSha256);
        // The mismatched file is kept (not deleted) so the caller can still install from it if the user chooses to.
        Assert.True(File.Exists(result.LocalPath));
    }

    [Fact]
    public async Task DownloadAsync_HashMatches_ReturnsSuccess()
    {
        var content = new byte[] { 5, 6, 7 };
        var expectedHash = await ComputeExpectedHashAsync(content);
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
        using var service = MakeService(handler);
        var entry = MakeEntry("https://example.com/app.exe", expectedSha256: expectedHash);

        var result = await service.DownloadAsync(entry, _cacheDir, null, CancellationToken.None);

        Assert.Equal(WebDownloadOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task DownloadAsync_InvalidUrl_ReturnsFailedWithoutCallingHttp()
    {
        var handler = new FakeHandler(_ => throw new InvalidOperationException("HTTP should not be called for an invalid URL"));
        using var service = MakeService(handler);
        var entry = MakeEntry("not a url");

        var result = await service.DownloadAsync(entry, _cacheDir, null, CancellationToken.None);

        Assert.Equal(WebDownloadOutcome.Failed, result.Outcome);
    }

    private static async Task<string> ComputeExpectedHashAsync(byte[] content)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(tempFile, content);
            return await Sha256Service.ComputeAsync(tempFile, CancellationToken.None);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }
}
