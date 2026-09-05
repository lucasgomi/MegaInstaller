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
        catch (Exception)
        {
            // Icon.ExtractAssociatedIcon shells out to the OS icon cache and
            // can fail in ways beyond IOException/ArgumentException (e.g. a
            // Win32Exception from the underlying shell call) depending on
            // the file and the machine - this method's whole contract is
            // "best effort, null if it doesn't work", so nothing should ever
            // escape uncaught and abort whatever the caller does next (this
            // silently broke type detection too, since it ran right after).
            return null;
        }
    }
}
