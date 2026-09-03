namespace MegaInstaller.Core.Models;

public sealed class InstallResult
{
    public required string EntryId { get; init; }
    public required string Name { get; init; }
    public required InstallOutcome Outcome { get; init; }
    public int? ExitCode { get; init; }
    public string? Message { get; init; }
}
