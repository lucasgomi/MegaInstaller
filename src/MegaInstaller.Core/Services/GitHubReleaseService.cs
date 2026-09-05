using System.Text.Json;

namespace MegaInstaller.Core.Services;

/// <summary>The bits of a GitHub release the app actually needs, already pulled out of the raw API JSON.</summary>
public sealed record GitHubReleaseInfo(string TagName, string HtmlUrl, string Body, string? ExeDownloadUrl, DateTimeOffset PublishedAt);

/// <summary>
/// Talks to GitHub's public REST API for release info - no token needed
/// for a public repo, just a User-Agent header (GitHub rejects requests
/// without one). Used both for the update checker and the in-app changelog.
/// </summary>
public sealed class GitHubReleaseService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public GitHubReleaseService() : this(CreateDefaultClient(), ownsClient: true)
    {
    }

    /// <summary>Uses a caller-supplied client instead of owning one - for tests (a fake handler). Never disposed by this instance.</summary>
    public GitHubReleaseService(HttpClient httpClient) : this(httpClient, ownsClient: false)
    {
    }

    private GitHubReleaseService(HttpClient httpClient, bool ownsClient)
    {
        _httpClient = httpClient;
        _ownsClient = ownsClient;
    }

    private static HttpClient CreateDefaultClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MegaInstaller");
        return client;
    }

    /// <summary>
    /// The latest published (non-draft, non-prerelease) release, or null if
    /// it couldn't be fetched - offline, GitHub unreachable, unexpected
    /// response. Never throws for those cases; a genuinely cancelled
    /// <paramref name="cancellationToken"/> still propagates as usual.
    /// </summary>
    public async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(string owner, string repo, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient
                .GetAsync($"https://api.github.com/repos/{owner}/{repo}/releases/latest", cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return Parse(doc.RootElement);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient surfaces its own request timeout as TaskCanceledException too; only a
            // genuinely cancelled token should propagate, this is just "couldn't check right now".
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    private static GitHubReleaseInfo Parse(JsonElement root)
    {
        var tagName = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() ?? string.Empty : string.Empty;
        var htmlUrl = root.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() ?? string.Empty : string.Empty;
        var body = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? string.Empty : string.Empty;
        var publishedAt = root.TryGetProperty("published_at", out var pubEl) && pubEl.TryGetDateTimeOffset(out var dto)
            ? dto
            : DateTimeOffset.MinValue;

        string? exeUrl = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                if (name is not null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    exeUrl = asset.TryGetProperty("browser_download_url", out var downloadEl) ? downloadEl.GetString() : null;
                    break;
                }
            }
        }

        return new GitHubReleaseInfo(tagName, htmlUrl, body, exeUrl, publishedAt);
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }
}
