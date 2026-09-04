using MegaInstaller.Core.Models;

namespace MegaInstaller.Core.Services;

/// <summary>
/// Turns an <see cref="InstallerEntry"/> into the actual (file, arguments)
/// pair that gets executed. Kept pure/side-effect-free so it can be unit
/// tested without touching the OS process APIs.
/// </summary>
public static class InstallCommandBuilder
{
    public static (string FileName, string Arguments) Build(string folder, InstallerEntry entry)
    {
        var fullPath = Path.Combine(folder, entry.FileName);
        var extension = Path.GetExtension(entry.FileName).ToLowerInvariant();

        // .msi/.msix/.msu aren't executables: Windows installs them through
        // msiexec, Add-AppxPackage and wusa respectively, so the entry's own
        // Arguments are appended to that host's command line instead.
        return extension switch
        {
            ".msi" => ("msiexec.exe", Append($"/i \"{fullPath}\"", entry.Arguments)),
            ".msix" or ".appx" or ".msixbundle" or ".appxbundle" => BuildAppxCommand(fullPath, entry),
            ".msu" => ("wusa.exe", Append($"\"{fullPath}\"", DefaultIfEmpty(entry.Arguments, "/quiet /norestart"))),
            _ => (fullPath, entry.Arguments ?? string.Empty),
        };
    }

    private static (string FileName, string Arguments) BuildAppxCommand(string fullPath, InstallerEntry entry)
    {
        // Single-quoted inside the PowerShell command, with embedded single
        // quotes doubled, so paths containing quotes can't break out of it.
        var escapedPath = fullPath.Replace("'", "''");
        var command = $"Add-AppxPackage -Path '{escapedPath}'";
        if (!string.IsNullOrWhiteSpace(entry.Arguments))
        {
            command += " " + entry.Arguments;
        }

        return ("powershell.exe", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command}\"");
    }

    private static string Append(string baseArguments, string? extra) =>
        string.IsNullOrWhiteSpace(extra) ? baseArguments : $"{baseArguments} {extra}";

    private static string DefaultIfEmpty(string? arguments, string fallback) =>
        string.IsNullOrWhiteSpace(arguments) ? fallback : arguments;
}
