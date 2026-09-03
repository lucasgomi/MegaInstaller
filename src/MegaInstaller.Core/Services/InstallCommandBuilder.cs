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
        var isMsi = string.Equals(Path.GetExtension(entry.FileName), ".msi", StringComparison.OrdinalIgnoreCase);

        if (!isMsi)
        {
            return (fullPath, entry.Arguments ?? string.Empty);
        }

        var args = $"/i \"{fullPath}\"";
        if (!string.IsNullOrWhiteSpace(entry.Arguments))
        {
            args += " " + entry.Arguments;
        }

        return ("msiexec.exe", args);
    }
}
