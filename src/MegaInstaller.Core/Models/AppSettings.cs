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

    /// <summary>Which look the app renders with. Read once at startup, so switching it needs a restart.</summary>
    public UiThemeMode UiTheme { get; set; } = UiThemeMode.Modern;

    /// <summary>Shows the diagnosis panel in the install window when something fails to install.</summary>
    public bool TroubleshooterEnabled { get; set; }

    /// <summary>Stops the "restart as administrator to get a single UAC prompt" offer from appearing before a batch.</summary>
    public bool SkipElevationOffer { get; set; }
}
