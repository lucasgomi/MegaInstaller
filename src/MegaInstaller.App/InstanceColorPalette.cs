namespace MegaInstaller.App;

/// <summary>
/// A small, curated set of accent colors instances can pick from (see
/// EditInstanceForm's color picker) instead of a raw color dialog - picking
/// from a fixed, hand-tuned palette means any choice still looks good next
/// to the app's own accent and next to every other instance's color.
/// </summary>
public static class InstanceColorPalette
{
    public static readonly IReadOnlyList<string> Colors = new[]
    {
        "#5B5FEF", // Índigo
        "#4F8EF7", // Azul
        "#3EC1C9", // Turquesa
        "#55C08A", // Verde
        "#F2994A", // Naranja
        "#EF6461", // Rojo
        "#EC6FA6", // Rosa
        "#9B6BF2", // Morado
    };

    /// <summary>Parses a stored "#RRGGBB" value, falling back to <paramref name="fallback"/> when null/invalid.</summary>
    public static Color Resolve(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        try
        {
            return ColorTranslator.FromHtml(hex);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or IndexOutOfRangeException)
        {
            return fallback;
        }
    }

    /// <summary>A pastel tint of a color, for badge/pill backgrounds that keep a dark icon or text legible on top.</summary>
    public static Color Tint(Color color) => ControlPaint.Light(color, 0.85f);
}
