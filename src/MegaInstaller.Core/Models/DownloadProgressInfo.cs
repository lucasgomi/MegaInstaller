namespace MegaInstaller.Core.Models;

public sealed class DownloadProgressInfo
{
    public DownloadProgressInfo(long bytesReceived, long? totalBytes, TimeSpan elapsed)
    {
        BytesReceived = bytesReceived;
        TotalBytes = totalBytes;
        Elapsed = elapsed;
    }

    public long BytesReceived { get; }
    public long? TotalBytes { get; }
    public TimeSpan Elapsed { get; }

    public double? PercentComplete =>
        TotalBytes is > 0 ? Math.Clamp(BytesReceived * 100.0 / TotalBytes.Value, 0, 100) : null;

    public double BytesPerSecond =>
        Elapsed.TotalSeconds > 0 ? BytesReceived / Elapsed.TotalSeconds : 0;
}
