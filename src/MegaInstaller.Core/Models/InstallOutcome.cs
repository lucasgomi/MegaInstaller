namespace MegaInstaller.Core.Models;

public enum InstallOutcome
{
    Success,
    SuccessRebootRequired,
    Failed,
    Cancelled,
    FileNotFound
}
