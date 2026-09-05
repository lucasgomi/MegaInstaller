using System.Net;
using System.Text;
using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

public class GitHubReleaseServiceTests
{
    private const string SampleReleaseJson = """
        {
          "tag_name": "v16",
          "html_url": "https://github.com/lucasgomi/MegaInstaller/releases/tag/v16",
          "body": "## Novedades\n- Cosa nueva",
          "published_at": "2026-09-04T20:00:00Z",
          "assets": [
            { "name": "MegaInstaller-v16-win-x64.zip", "browser_download_url": "https://example.com/x.zip" },
            { "name": "MegaInstaller-v16-win-x64.exe", "browser_download_url": "https://example.com/x.exe" }
          ]
        }
        """;

    private static GitHubReleaseService MakeService(HttpMessageHandler handler) => new(new HttpClient(handler));

    [Fact]
    public async Task GetLatestReleaseAsync_Success_ParsesAllFields()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleReleaseJson, Encoding.UTF8, "application/json"),
        });
        using var service = MakeService(handler);

        var result = await service.GetLatestReleaseAsync("lucasgomi", "MegaInstaller", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("v16", result!.TagName);
        Assert.Equal("https://github.com/lucasgomi/MegaInstaller/releases/tag/v16", result.HtmlUrl);
        Assert.Contains("Novedades", result.Body);
        Assert.Equal("https://example.com/x.exe", result.ExeDownloadUrl);
        Assert.Equal(new DateTimeOffset(2026, 9, 4, 20, 0, 0, TimeSpan.Zero), result.PublishedAt);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_HttpError_ReturnsNull()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var service = MakeService(handler);

        var result = await service.GetLatestReleaseAsync("lucasgomi", "MegaInstaller", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_MalformedJson_ReturnsNullWithoutThrowing()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not json", Encoding.UTF8, "application/json"),
        });
        using var service = MakeService(handler);

        var result = await service.GetLatestReleaseAsync("lucasgomi", "MegaInstaller", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_NoExeAsset_ExeDownloadUrlIsNull()
    {
        const string json = """{ "tag_name": "v16", "html_url": "u", "body": "", "assets": [] }""";
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        using var service = MakeService(handler);

        var result = await service.GetLatestReleaseAsync("lucasgomi", "MegaInstaller", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Null(result!.ExeDownloadUrl);
    }

    [Fact]
    public async Task GetLatestReleaseAsync_SendsUserAgentHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new FakeHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(SampleReleaseJson) };
        });
        using var client = new HttpClient(handler); // Not using the default client, so no User-Agent unless the caller sets one explicitly.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MegaInstaller");
        using var service = new GitHubReleaseService(client);

        await service.GetLatestReleaseAsync("lucasgomi", "MegaInstaller", CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.NotEmpty(capturedRequest!.Headers.UserAgent);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }
}
