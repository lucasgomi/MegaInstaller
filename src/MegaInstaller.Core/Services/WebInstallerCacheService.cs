using MegaInstaller.Core.Models;

namespace MegaInstaller.Core.Services;

public enum WebDownloadOutcome
{
    Success,
    Failed,
    HashMismatch,
}

/// <summary>Outcome of resolving one web-sourced <see cref="InstallerEntry"/> to a local file.</summary>
public sealed record WebDownloadResult(InstallerEntry Entry, WebDownloadOutcome Outcome, string? LocalPath, string? ErrorMessage, string? ActualSha256)
{
    public static WebDownloadResult Success(InstallerEntry entry, string path) => new(entry, WebDownloadOutcome.Success, path, null, null);

    public static WebDownloadResult Failed(InstallerEntry entry, string error) => new(entry, WebDownloadOutcome.Failed, null, error, null);

    public static WebDownloadResult HashMismatch(InstallerEntry entry, string path, string actualSha256) => new(entry, WebDownloadOutcome.HashMismatch, path, null, actualSha256);
}

/// <summary>
/// Resolves a web-sourced <see cref="InstallerEntry"/> (see
/// <see cref="InstallerEntry.MirrorUrl"/>) to an actual local file by
/// downloading it into a cache folder, so it never has to travel on the
/// installers folder/USB itself. Wraps <see cref="DownloadService"/> rather
/// than talking HTTP directly, so both share the exact same download logic.
/// </summary>
public sealed class WebInstallerCacheService : IDisposable
{
    public const string DefaultCacheFolderName = "WebCache";

    private readonly DownloadService _downloadService;
    private readonly bool _ownsDownloadService;

    public WebInstallerCacheService() : this(new DownloadService(), ownsDownloadService: true)
    {
    }

    /// <summary>Uses a caller-supplied <see cref="DownloadService"/> instead of owning one - for tests. Never disposed by this instance.</summary>
    public WebInstallerCacheService(DownloadService downloadService) : this(downloadService, ownsDownloadService: false)
    {
    }

    private WebInstallerCacheService(DownloadService downloadService, bool ownsDownloadService)
    {
        _downloadService = downloadService;
        _ownsDownloadService = ownsDownloadService;
    }

    /// <summary>Where downloads land: the user's override from Ajustes if set, otherwise a predictable default under %LocalAppData%.</summary>
    public static string ResolveCacheFolder(AppSettings settings) =>
        string.IsNullOrWhiteSpace(settings.WebCacheFolder)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MegaInstaller", DefaultCacheFolderName)
            : settings.WebCacheFolder;

    /// <summary>
    /// Downloads <paramref name="entry"/>'s mirror into <paramref name="cacheFolder"/>
    /// and verifies its pinned hash, if any. Throws <see cref="OperationCanceledException"/>
    /// on cancellation; every other failure comes back as a <see cref="WebDownloadResult"/>
    /// instead of an exception, so a caller driving several downloads in a
    /// loop doesn't need a try/catch around each one.
    /// </summary>
    public async Task<WebDownloadResult> DownloadAsync(
        InstallerEntry entry,
        string cacheFolder,
        IProgress<DownloadProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entry.MirrorUrl) || !Uri.TryCreate(entry.MirrorUrl, UriKind.Absolute, out var uri))
        {
            return WebDownloadResult.Failed(entry, "El mirror configurado no es una URL válida.");
        }

        var destinationPath = Path.Combine(cacheFolder, entry.FileName);

        try
        {
            await _downloadService.DownloadAsync(uri, destinationPath, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            TryDelete(destinationPath);
            return WebDownloadResult.Failed(entry, ex.Message);
        }

        var info = new FileInfo(destinationPath);
        if (!info.Exists || info.Length == 0)
        {
            TryDelete(destinationPath);
            return WebDownloadResult.Failed(entry, "El mirror devolvió un archivo vacío.");
        }

        if (!string.IsNullOrWhiteSpace(entry.ExpectedSha256))
        {
            var actual = await Sha256Service.ComputeAsync(destinationPath, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(actual, entry.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                return WebDownloadResult.HashMismatch(entry, destinationPath, actual);
            }
        }

        return WebDownloadResult.Success(entry, destinationPath);
    }

    /// <summary>Deletes everything under the cache folder; best-effort, called after a run when the user wants no leftovers.</summary>
    public static void ClearCache(string cacheFolder)
    {
        try
        {
            if (Directory.Exists(cacheFolder))
            {
                Directory.Delete(cacheFolder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public void Dispose()
    {
        if (_ownsDownloadService)
        {
            _downloadService.Dispose();
        }
    }
}
