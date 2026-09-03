using MegaInstaller.Core.Models;

namespace MegaInstaller.Core.Services;

/// <summary>Finds installer files sitting in a folder that the manifest doesn't know about yet.</summary>
public static class FolderScanner
{
    private static readonly string[] InstallerExtensions = { ".exe", ".msi" };

    public static IReadOnlyList<string> FindUntrackedInstallers(string folder, InstallerManifest manifest)
    {
        if (!Directory.Exists(folder))
        {
            return Array.Empty<string>();
        }

        var known = new HashSet<string>(
            manifest.Items.Select(i => i.FileName),
            StringComparer.OrdinalIgnoreCase);

        return Directory.EnumerateFiles(folder)
            .Select(Path.GetFileName)
            .Where(name => name is not null)
            .Select(name => name!)
            .Where(name => InstallerExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
            .Where(name => !string.Equals(name, ManifestService.ManifestFileName, StringComparison.OrdinalIgnoreCase))
            .Where(name => !known.Contains(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
