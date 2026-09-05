using System.IO.Compression;
using System.Text.Json;
using MegaInstaller.Core.Models;

namespace MegaInstaller.Core.Services;

/// <summary>What importing a package would do, computed without touching disk - shown to the user before they commit to it.</summary>
public sealed record ImportPreview(
    IReadOnlyList<InstanceDefinition> NewInstances,
    IReadOnlyList<InstanceDefinition> SkippedInstances,
    IReadOnlyList<InstallerEntry> NewInstallers,
    IReadOnlyList<InstallerEntry> SkippedInstallers,
    IReadOnlyList<string> RenamedFiles);

/// <summary>
/// Packages one instance or an entire manifest into a portable .zip - its
/// installers' files, any custom icons it uses, and the manifest slice
/// needed to reconstruct it - and imports one back into a (possibly
/// different) installers folder. Web-sourced installers (see
/// <see cref="InstallerEntry.MirrorUrl"/>) never carry a file either way;
/// that's the entire point of them. Import is idempotent (an entry or
/// instance whose Id already exists is skipped, so re-importing the same
/// package twice never duplicates anything) and never overwrites an
/// unrelated existing file - a filename collision gets a " (2)"-style
/// rename instead.
/// </summary>
public static class ExportPackageService
{
    private const string ManifestEntryName = "package-manifest.json";
    private const string FilesFolder = "files";
    private const string IconsFolder = "icons";

    public static void ExportInstance(string folder, InstallerManifest manifest, InstanceDefinition instance, string destinationZipPath)
    {
        var package = new InstallerManifest
        {
            Items = InstanceService.ResolveInstallers(manifest, instance).ToList(),
            Instances = { instance },
        };
        WritePackage(folder, package, destinationZipPath);
    }

    public static void ExportAll(string folder, InstallerManifest manifest, string destinationZipPath) =>
        WritePackage(folder, manifest, destinationZipPath);

    private static void WritePackage(string folder, InstallerManifest package, string destinationZipPath)
    {
        if (File.Exists(destinationZipPath))
        {
            File.Delete(destinationZipPath);
        }

        var destinationDir = Path.GetDirectoryName(destinationZipPath);
        if (!string.IsNullOrEmpty(destinationDir))
        {
            Directory.CreateDirectory(destinationDir);
        }

        using var archive = ZipFile.Open(destinationZipPath, ZipArchiveMode.Create);

        var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
        using (var stream = manifestEntry.Open())
        {
            JsonSerializer.Serialize(stream, package, ManifestService.JsonOptions);
        }

        foreach (var item in package.Items)
        {
            if (!string.IsNullOrWhiteSpace(item.MirrorUrl))
            {
                continue;
            }

            var sourcePath = Path.Combine(folder, item.FileName);
            if (File.Exists(sourcePath))
            {
                archive.CreateEntryFromFile(sourcePath, $"{FilesFolder}/{item.FileName}", CompressionLevel.Optimal);
            }
        }

        var packagedIcons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var instance in package.Instances)
        {
            var iconFileName = CustomIconPaths.FileNameFromKey(instance.IconKey);
            if (iconFileName is null || !packagedIcons.Add(iconFileName))
            {
                continue;
            }

            var sourcePath = Path.Combine(folder, CustomIconPaths.FolderName, iconFileName);
            if (File.Exists(sourcePath))
            {
                archive.CreateEntryFromFile(sourcePath, $"{IconsFolder}/{iconFileName}", CompressionLevel.Optimal);
            }
        }
    }

    /// <summary>Throws <see cref="InvalidDataException"/> if the zip isn't a MegaInstaller export package.</summary>
    public static ImportPreview PreviewImport(string zipPath, string destinationFolder)
    {
        var package = ReadPackageManifest(zipPath);
        var destination = new ManifestService().Load(destinationFolder);

        var existingInstanceIds = destination.Instances.Select(i => i.Id).ToHashSet();
        var existingItemIds = destination.Items.Select(i => i.Id).ToHashSet();

        var newInstances = package.Instances.Where(i => !existingInstanceIds.Contains(i.Id)).ToList();
        var skippedInstances = package.Instances.Where(i => existingInstanceIds.Contains(i.Id)).ToList();
        var newInstallers = package.Items.Where(i => !existingItemIds.Contains(i.Id)).ToList();
        var skippedInstallers = package.Items.Where(i => existingItemIds.Contains(i.Id)).ToList();

        var existingFileNames = ExistingFileNames(destinationFolder);
        var renamedFiles = newInstallers
            .Where(i => string.IsNullOrWhiteSpace(i.MirrorUrl) && existingFileNames.Contains(i.FileName))
            .Select(i => i.FileName)
            .ToList();

        return new ImportPreview(newInstances, skippedInstances, newInstallers, skippedInstallers, renamedFiles);
    }

    /// <summary>Throws <see cref="InvalidDataException"/> if the zip isn't a MegaInstaller export package.</summary>
    public static void Import(string zipPath, string destinationFolder)
    {
        Directory.CreateDirectory(destinationFolder);
        var package = ReadPackageManifest(zipPath);
        var manifestService = new ManifestService();
        var destination = manifestService.Load(destinationFolder);

        var existingInstanceIds = destination.Instances.Select(i => i.Id).ToHashSet();
        var existingItemIds = destination.Items.Select(i => i.Id).ToHashSet();
        var existingFileNames = ExistingFileNames(destinationFolder);

        using var archive = ZipFile.OpenRead(zipPath);

        foreach (var item in package.Items)
        {
            if (existingItemIds.Contains(item.Id))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.MirrorUrl))
            {
                var targetFileName = UniqueName(item.FileName, existingFileNames);
                var entry = archive.GetEntry($"{FilesFolder}/{item.FileName}");
                if (entry is not null)
                {
                    entry.ExtractToFile(Path.Combine(destinationFolder, targetFileName), overwrite: false);
                    existingFileNames.Add(targetFileName);
                }

                item.FileName = targetFileName;
            }

            destination.Items.Add(item);
        }

        var iconsDir = Path.Combine(destinationFolder, CustomIconPaths.FolderName);
        var existingIconNames = ExistingFileNames(iconsDir);

        foreach (var instance in package.Instances)
        {
            if (existingInstanceIds.Contains(instance.Id))
            {
                continue;
            }

            var iconFileName = CustomIconPaths.FileNameFromKey(instance.IconKey);
            if (iconFileName is not null)
            {
                var entry = archive.GetEntry($"{IconsFolder}/{iconFileName}");
                if (entry is not null)
                {
                    var targetIconName = UniqueName(iconFileName, existingIconNames);
                    Directory.CreateDirectory(iconsDir);
                    entry.ExtractToFile(Path.Combine(iconsDir, targetIconName), overwrite: false);
                    existingIconNames.Add(targetIconName);
                    instance.IconKey = CustomIconPaths.BuildKey(targetIconName);
                }
            }

            destination.Instances.Add(instance);
        }

        manifestService.Save(destinationFolder, destination);
    }

    private static InstallerManifest ReadPackageManifest(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.GetEntry(ManifestEntryName)
            ?? throw new InvalidDataException("El archivo no es un paquete de MegaInstaller (falta package-manifest.json).");

        using var stream = entry.Open();
        return JsonSerializer.Deserialize<InstallerManifest>(stream, ManifestService.JsonOptions)
            ?? throw new InvalidDataException("El paquete de MegaInstaller está vacío o dañado.");
    }

    private static HashSet<string> ExistingFileNames(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return Directory.GetFiles(folder)
            .Select(path => Path.GetFileName(path)!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The given name if it's free, otherwise "name (2).ext", "name (3).ext", ... - never overwrites an unrelated file.</summary>
    private static string UniqueName(string fileName, HashSet<string> existing)
    {
        if (existing.Add(fileName))
        {
            return fileName;
        }

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var i = 2; ; i++)
        {
            var candidate = $"{baseName} ({i}){extension}";
            if (existing.Add(candidate))
            {
                return candidate;
            }
        }
    }
}
