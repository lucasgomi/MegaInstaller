using System.Diagnostics;
using MegaInstaller.Core.Models;

namespace MegaInstaller.Core.Services;

/// <summary>Downloads a file over HTTP(S) while reporting live progress.</summary>
public sealed class DownloadService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    // HttpClient's default 100s Timeout aborts a real-world installer
    // (100MB+ is common - Discord's own is 107MB) on anything but a fast
    // connection, throwing a TaskCanceledException indistinguishable from a
    // deliberate cancel unless callers check IsCancellationRequested (see
    // AddWebInstallerForm/InstallProgressForm). Downloads are already
    // cancellable and progress-reported via the caller's own
    // CancellationToken, so nothing needs HttpClient's own opinionated cap.
    public DownloadService() : this(new HttpClient { Timeout = Timeout.InfiniteTimeSpan }, ownsClient: true)
    {
    }

    /// <summary>Uses a caller-supplied client instead of owning one - for tests (a fake handler) or to share a client elsewhere. Never disposed by this instance.</summary>
    public DownloadService(HttpClient httpClient) : this(httpClient, ownsClient: false)
    {
    }

    private DownloadService(HttpClient httpClient, bool ownsClient)
    {
        _httpClient = httpClient;
        _ownsClient = ownsClient;
    }

    public async Task DownloadAsync(
        Uri url,
        string destinationPath,
        IProgress<DownloadProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var httpStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var fileStream = new FileStream(
            destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);

        var buffer = new byte[81920];
        long totalRead = 0;
        var stopwatch = Stopwatch.StartNew();

        int read;
        while ((read = await httpStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            totalRead += read;
            progress?.Report(new DownloadProgressInfo(totalRead, totalBytes, stopwatch.Elapsed));
        }
    }

    public void Dispose() => _httpClient.Dispose();
}
