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
        ("gift-fill", "Otros"),
    };

    private static readonly Dictionary<string, Image?> Cache = new();

    /// <summary>Returns the cached icon image for a key, or null if the key is empty/unknown.</summary>
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
