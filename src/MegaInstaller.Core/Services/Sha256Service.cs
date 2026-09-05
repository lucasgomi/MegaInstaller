using System.Security.Cryptography;

namespace MegaInstaller.Core.Services;

/// <summary>Computes the SHA-256 of a file for integrity pinning (see <see cref="Models.InstallerEntry.ExpectedSha256"/>).</summary>
public static class Sha256Service
{
    public static async Task<string> ComputeAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
