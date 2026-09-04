namespace MegaInstaller.Core.Models;

/// <summary>Small per-user app settings, independent from any installers folder.</summary>
public sealed class AppSettings
{
    public string? LastFolder { get; set; }

    /// <summary>
    /// Windows logon session id (see Process.SessionId) last seen at startup.
    /// Used to show the folder picker once per Windows session instead of
    /// on every single launch.
    /// </summary>
    public int? LastWindowsSessionId { get; set; }
}
