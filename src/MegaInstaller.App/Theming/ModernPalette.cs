namespace MegaInstaller.App.Theming;

/// <summary>Flat, soft-indigo palette for <see cref="UiThemeMode.Modern"/>. Classic mode never reads this.</summary>
public static class ModernPalette
{
    public static readonly Color Background = Color.FromArgb(0xF4, 0xF5, 0xFA);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(0xE3, 0xE5, 0xEC);

    public static readonly Color Accent = Color.FromArgb(0x5B, 0x5F, 0xEF);
    public static readonly Color AccentHover = Color.FromArgb(0x4A, 0x4E, 0xDB);
    public static readonly Color AccentPressed = Color.FromArgb(0x3D, 0x40, 0xC4);
    public static readonly Color AccentSoft = Color.FromArgb(0xEC, 0xEC, 0xFD);

    public static readonly Color TextPrimary = Color.FromArgb(0x1F, 0x24, 0x2B);
    public static readonly Color TextSecondary = Color.FromArgb(0x6B, 0x72, 0x80);
    public static readonly Color OnAccent = Color.White;
}
