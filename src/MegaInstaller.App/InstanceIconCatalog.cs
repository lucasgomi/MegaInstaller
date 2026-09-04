using System.Reflection;

namespace MegaInstaller.App;

/// <summary>
/// The small, bundled icon pack instances can pick from (Bootstrap Icons,
/// MIT-licensed - see Resources/InstanceIcons/THIRD-PARTY-NOTICES.md),
/// embedded as resources so no extra files ship alongside the exe.
/// </summary>
public static class InstanceIconCatalog
{
    public static readonly IReadOnlyList<(string Key, string DisplayName)> Icons = new (string, string)[]
    {
        ("box-seam-fill", "Genérico"),
        ("controller", "Juegos"),
        ("briefcase-fill", "Oficina"),
        ("code-slash", "Desarrollo"),
        ("terminal-fill", "Línea de comandos"),
        ("camera-reels-fill", "Multimedia"),
        ("music-note-beamed", "Audio"),
        ("image-fill", "Gráficos"),
        ("palette-fill", "Diseño"),
        ("globe2", "Internet/Navegador"),
        ("chat-dots-fill", "Comunicación"),
        ("envelope-fill", "Correo"),
        ("shield-lock-fill", "Seguridad"),
        ("cloud-fill", "Nube/Backup"),
        ("hdd-network-fill", "Red"),
        ("cpu-fill", "Sistema"),
        ("display", "Escritorio"),
        ("printer-fill", "Impresión/Drivers"),
        ("wrench-adjustable", "Utilidades"),
        ("folder2-open", "Archivos"),
        ("calculator-fill", "Productividad"),
        ("book-fill", "Educación"),
        ("puzzle-fill", "Complementos"),
        ("camera-fill", "Fotografía"),
        ("film", "Cine"),
        ("tv-fill", "TV/Streaming"),
        ("trophy-fill", "Deportes/Logros"),
        ("heart-pulse-fill", "Salud/Fitness"),
        ("bank", "Finanzas"),
        ("piggy-bank-fill", "Ahorro"),
        ("cart-fill", "Compras"),
        ("airplane-fill", "Viajes"),
        ("car-front-fill", "Vehículos"),
        ("house-fill", "Hogar"),
        ("mortarboard-fill", "Estudios"),
        ("phone-fill", "Móvil"),
        ("server", "Servidores"),
        ("database-fill", "Bases de datos"),
        ("clipboard-data-fill", "Datos/Informes"),
        ("people-fill", "Social/Equipos"),
        ("star-fill", "Favoritos"),
        ("lightning-charge-fill", "Rendimiento"),
        ("moon-stars-fill", "Modo noche"),
        ("rocket-takeoff-fill", "Productividad+"),
        ("gift-fill", "Otros"),
    };

    /// <summary>Subfolder (inside the installers folder) where custom-uploaded icons live.</summary>
    public const string CustomThemeFolderName = "CustomTheme";

    private const string CustomKeyPrefix = "custom:";

    private static readonly Dictionary<string, Image?> Cache = new();

    /// <summary>Returns the cached icon image for a built-in key, or null if the key is empty/unknown.</summary>
    public static Image? Load(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var image = LoadFromResource(key);
        Cache[key] = image;
        return image;
    }

    /// <summary>Builds the IconKey for a custom icon file already saved under CustomTheme.</summary>
    public static string CustomKey(string fileName) => CustomKeyPrefix + fileName;

    /// <summary>True when an IconKey refers to a custom-uploaded photo rather than a built-in glyph.</summary>
    public static bool IsCustomKey(string? key) => key is not null && key.StartsWith(CustomKeyPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Resolves an instance's IconKey, which is either a built-in key (see
    /// <see cref="Load"/>) or a "custom:filename.png" reference to a file
    /// under "&lt;folder&gt;/CustomTheme/". Custom icons aren't cached since
    /// they live on disk and can be replaced or removed independently of
    /// the running app.
    /// </summary>
    public static Image? LoadForInstance(string? key, string folder)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return key.StartsWith(CustomKeyPrefix, StringComparison.Ordinal)
            ? LoadCustomIcon(folder, key[CustomKeyPrefix.Length..])
            : Load(key);
    }

    /// <summary>File names (without the "custom:" prefix) of every custom icon already uploaded for this folder.</summary>
    public static IReadOnlyList<string> ListCustomIcons(string folder)
    {
        var directory = Path.Combine(folder, CustomThemeFolderName);
        if (!Directory.Exists(directory))
        {
            return Array.Empty<string>();
        }

        try
        {
            return Directory.GetFiles(directory, "*.png")
                .Select(Path.GetFileName)
                .Where(name => name is not null)
                .Cast<string>()
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static Image? LoadCustomIcon(string folder, string fileName)
    {
        try
        {
            var path = Path.Combine(folder, CustomThemeFolderName, fileName);
            if (!File.Exists(path))
            {
                return null;
            }

            // Read via a detached MemoryStream rather than Image.FromFile,
            // which keeps the source file locked for as long as the Image
            // is alive - that would block re-cropping/overwriting it later.
            var bytes = File.ReadAllBytes(path);
            using var stream = new MemoryStream(bytes);
            return Image.FromStream(stream);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static Image? LoadFromResource(string key)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream($"InstanceIcons.{key}.png");
            return stream is null ? null : Image.FromStream(stream);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            return null;
        }
    }
}
