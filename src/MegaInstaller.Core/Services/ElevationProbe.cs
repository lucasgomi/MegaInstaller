using System.Runtime.Versioning;
using System.Security.Principal;

namespace MegaInstaller.Core.Services;

/// <summary>
/// Whether this process is already running elevated. It matters because a
/// child process inherits its parent's token: launched from an elevated
/// MegaInstaller, an installer runs elevated with no UAC prompt of its own,
/// so one consent at startup covers the whole batch instead of one prompt
/// per installer.
/// </summary>
public static class ElevationProbe
{
    private static bool? _cached;

    /// <summary>False on non-Windows (and if the check itself fails), which just means "keep asking per installer".</summary>
    public static bool IsProcessElevated() => _cached ??= Probe();

    [SupportedOSPlatform("windows")]
    private static bool ProbeWindows()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static bool Probe()
    {
        try
        {
            return OperatingSystem.IsWindows() && ProbeWindows();
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
