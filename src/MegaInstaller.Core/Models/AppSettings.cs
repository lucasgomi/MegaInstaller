namespace MegaInstaller.Core.Models;

/// <summary>Small per-user app settings, independent from any installers folder.</summary>
public sealed class AppSettings
{
    public string? LastFolder { get; set; }
    public bool StopOnError { get; set; }
}
