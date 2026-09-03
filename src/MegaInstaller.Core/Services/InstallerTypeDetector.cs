using System.Text;
using MegaInstaller.Core.Models;

namespace MegaInstaller.Core.Services;

/// <summary>
/// Best-effort installer-family detection: an .msi extension or the OLE
/// compound-file header is a certain match; everything else is a plain
/// byte-marker search over a bounded sample of the file. Never executes
/// the file - only ever reads it. When nothing matches, the caller should
/// fall back to <see cref="InstallerType.Unknown"/> (no silent flags
/// assumed) rather than guessing.
/// </summary>
public static class InstallerTypeDetector
{
    private static readonly byte[] MsiMagic = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };

    private const int HeadSampleBytes = 4 * 1024 * 1024;
    private const int TailSampleBytes = 1 * 1024 * 1024;

    public static InstallerType Detect(string filePath)
    {
        if (string.Equals(Path.GetExtension(filePath), ".msi", StringComparison.OrdinalIgnoreCase))
        {
            return InstallerType.Msi;
        }

        if (!File.Exists(filePath))
        {
            return InstallerType.Unknown;
        }

        try
        {
            using var stream = File.OpenRead(filePath);

            var header = new byte[MsiMagic.Length];
            var read = ReadFully(stream, header);
            if (read == header.Length && header.AsSpan().SequenceEqual(MsiMagic))
            {
                return InstallerType.Msi;
            }

            var sample = ReadSample(stream);

            if (Contains(sample, "Inno Setup"))
            {
                return InstallerType.InnoSetup;
            }

            if (Contains(sample, "Nullsoft"))
            {
                return InstallerType.Nsis;
            }

            if (Contains(sample, "InstallShield"))
            {
                return InstallerType.InstallShield;
            }
        }
        catch (IOException)
        {
            // Locked or otherwise inaccessible file: best effort, stay Unknown.
        }

        return InstallerType.Unknown;
    }

    private static int ReadFully(Stream stream, byte[] buffer)
    {
        var totalRead = 0;
        int read;
        while (totalRead < buffer.Length &&
               (read = stream.Read(buffer, totalRead, buffer.Length - totalRead)) > 0)
        {
            totalRead += read;
        }

        return totalRead;
    }

    private static byte[] ReadSample(FileStream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        var headLength = (int)Math.Min(HeadSampleBytes, stream.Length);
        var head = new byte[headLength];
        ReadFully(stream, head);

        if (stream.Length <= HeadSampleBytes)
        {
            return head;
        }

        var tailLength = (int)Math.Min(TailSampleBytes, stream.Length - headLength);
        var tail = new byte[tailLength];
        stream.Seek(-tailLength, SeekOrigin.End);
        ReadFully(stream, tail);

        var combined = new byte[head.Length + tail.Length];
        Buffer.BlockCopy(head, 0, combined, 0, head.Length);
        Buffer.BlockCopy(tail, 0, combined, head.Length, tail.Length);
        return combined;
    }

    private static bool Contains(byte[] haystack, string marker) =>
        IndexOf(haystack, Encoding.ASCII.GetBytes(marker)) >= 0;

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || haystack.Length < needle.Length)
        {
            return -1;
        }

        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var isMatch = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    isMatch = false;
                    break;
                }
            }

            if (isMatch)
            {
                return i;
            }
        }

        return -1;
    }
}
