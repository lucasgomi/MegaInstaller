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

    /// <summary>A button matching the active theme; pass primary:true for the one main action in a dialog.</summary>
    public static Button CreateButton(string text, bool primary = false)
    {
        Button button = IsModern ? new ModernButton { Primary = primary } : new Button();
        button.Text = text;
        button.AutoSize = true;
        button.Margin = new Padding(4);
        if (!IsModern && primary)
        {
            button.Font = new Font(button.Font, FontStyle.Bold);
        }

        return button;
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
