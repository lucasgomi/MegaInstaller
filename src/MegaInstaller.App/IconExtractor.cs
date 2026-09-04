namespace MegaInstaller.App;

/// <summary>Best-effort extraction of the icon Windows shows for a given file (exe/msi).</summary>
public static class IconExtractor
{
    public static Image? TryExtract(string fullPath)
    {
        try
        {
            if (!File.Exists(fullPath))
            {
                return null;
            }

            using var icon = Icon.ExtractAssociatedIcon(fullPath);
            return icon?.ToBitmap();
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
