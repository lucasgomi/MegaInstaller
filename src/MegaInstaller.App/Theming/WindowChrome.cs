using System.Runtime.InteropServices;

namespace MegaInstaller.App.Theming;

/// <summary>Windows 11's rounded window corners, applied best-effort for the Modern theme.</summary>
internal static class WindowChrome
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>
    /// Works for any control with its own native window - a top-level
    /// <see cref="Form"/>, but also a <see cref="ContextMenuStrip"/>'s popup,
    /// which WinForms renders as a plain square WS_POPUP window with no
    /// native Windows 11 styling of its own.
    /// </summary>
    public static void ApplyRoundedCorners(Control control)
    {
        if (control.IsHandleCreated)
        {
            TrySetRoundedCorners(control.Handle);
        }
        else
        {
            control.HandleCreated += (_, _) => TrySetRoundedCorners(control.Handle);
        }
    }

    private static void TrySetRoundedCorners(IntPtr handle)
    {
        try
        {
            var preference = DwmwcpRound;
            DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref preference, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Pre-Windows 11, or a locked-down environment without dwmapi.dll:
            // native square corners stay. Nothing else depends on this succeeding.
        }
    }
}
