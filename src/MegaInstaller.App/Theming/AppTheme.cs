using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.App.Theming;

/// <summary>
/// Single on/off switch for the whole app's look, read once at startup
/// (see Program.cs) before any form is built - forms consult
/// <see cref="IsModern"/> while constructing their own controls, so
/// switching themes takes effect on the next launch rather than live
/// (see SettingsForm's restart prompt).
/// </summary>
public static class AppTheme
{
    public static UiThemeMode Current { get; private set; } = UiThemeMode.Modern;

    public static bool IsModern => Current == UiThemeMode.Modern;

    public static void Initialize()
    {
        Current = new AppSettingsService(AppSettingsService.DefaultPath).Load().UiTheme;
    }

    /// <summary>Size a button icon is drawn at, and therefore the size it has to be stored at (see <see cref="CreateButton"/>).</summary>
    public const int ButtonIconSize = 16;

    /// <summary>A button matching the active theme; pass primary:true for the one main action in a dialog.</summary>
    public static Button CreateButton(string text, bool primary = false, Image? icon = null)
    {
        Button button = IsModern ? new ModernButton { Primary = primary } : new Button();
        button.Text = text;
        button.AutoSize = true;
        button.Margin = new Padding(4, 3, 4, 3);
        if (!IsModern && primary)
        {
            button.Font = new Font(button.Font, FontStyle.Bold);
        }

        if (icon is not null)
        {
            // Button.AutoSize measures Image at its own pixel size, so
            // handing it a 64px source icon makes the button ~72px tall and
            // blows out whatever fixed-height row it sits in (which is what
            // broke the header strip). Store it at the size it's drawn at.
            button.Image = ScaleIcon(icon, ButtonIconSize);
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.TextAlign = ContentAlignment.MiddleRight;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.Padding = new Padding(6, 0, 6, 0);
        }

        return button;
    }

    /// <summary>
    /// A button that shows a dropdown menu below itself instead of firing
    /// directly - used to group several related actions (e.g. Nueva/Editar/
    /// Eliminar instancia) behind one button instead of a row of separate ones.
    /// </summary>
    public static Button CreateDropdownButton(string text, ContextMenuStrip menu, bool primary = false)
    {
        var button = CreateButton($"{text}  ▾", primary);
        button.Click += (_, _) => menu.Show(button, new Point(0, button.Height + 2));
        return button;
    }

    /// <summary>
    /// A menu item for a dropdown/context menu. Deliberately plain - two
    /// rounds of custom Modern styling here (rounded corners, then just
    /// color/padding/font) both still read as misaligned, so this now
    /// renders with WinForms' own untouched default, which is already the
    /// native Windows look the same way File Explorer's menus are.
    /// </summary>
    public static ToolStripMenuItem CreateMenuItem(string text, EventHandler handler)
    {
        var item = new ToolStripMenuItem(text);
        item.Click += handler;
        return item;
    }

    private static Bitmap ScaleIcon(Image source, int size)
    {
        var scaled = new Bitmap(size, size);
        using var g = Graphics.FromImage(scaled);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        g.DrawImage(source, new Rectangle(0, 0, size, size));
        return scaled;
    }

    /// <summary>
    /// Applies the modern palette to a fully-built form: the form itself,
    /// every plain layout panel still at its default color (a deliberately
    /// custom-colored one, like a header strip, is left alone), and
    /// Windows 11's rounded corners. No-op in Classic. Call this last, after
    /// every child control has already been added.
    /// </summary>
    public static void StyleForm(Form form)
    {
        if (!IsModern) return;

        form.BackColor = ModernPalette.Background;
        RecolorDefaultPanels(form);
        WindowChrome.ApplyRoundedCorners(form);
    }

    private static void RecolorDefaultPanels(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is Panel && child.BackColor == SystemColors.Control)
            {
                child.BackColor = ModernPalette.Background;
            }

            RecolorDefaultPanels(child);
        }
    }
}
