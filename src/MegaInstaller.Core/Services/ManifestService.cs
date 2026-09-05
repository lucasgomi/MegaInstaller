using System.Text.Json;
using System.Text.Json.Serialization;
using MegaInstaller.Core.Exceptions;
using MegaInstaller.Core.Models;

namespace MegaInstaller.Core.Services;

/// <summary>
/// Reads and writes the megainstaller.json "information file" that lives
/// inside an installers folder. This is what lets the hub reinstall the
/// same batch automatically later, with the same flags, on any machine.
/// </summary>
public sealed class ManifestService
{
    public const string ManifestFileName = "megainstaller.json";

    /// <summary>Shared with <see cref="ExportPackageService"/> so a packaged manifest round-trips identically to megainstaller.json itself.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string GetManifestPath(string folder) => Path.Combine(folder, ManifestFileName);

    public bool ManifestExists(string folder) => File.Exists(GetManifestPath(folder));

    /// <summary>Loads the manifest, or returns an empty one if the file doesn't exist yet.</summary>
    /// <exception cref="ManifestException">The file exists but isn't valid JSON.</exception>
    public InstallerManifest Load(string folder)
    {
        var path = GetManifestPath(folder);
        if (!File.Exists(path))
        {
            return new InstallerManifest();
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new ManifestException($"No se pudo leer {ManifestFileName}.", ex);
        }

        try
        {
            return JsonSerializer.Deserialize<InstallerManifest>(json, JsonOptions) ?? new InstallerManifest();
        }
        catch (JsonException ex)
        {
            throw new ManifestException(
                $"El archivo {ManifestFileName} está dañado o no es un JSON válido.", ex);
        }
    }

    public void Save(string folder, InstallerManifest manifest)
    {
        Directory.CreateDirectory(folder);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        File.WriteAllText(GetManifestPath(folder), json);
    }
}
