using System.ComponentModel;
using System.Diagnostics;

namespace MegaInstaller.App;

/// <summary>
/// Restarts MegaInstaller elevated. Everything it launches afterwards
/// inherits that token, so one UAC prompt covers every installer in a batch
/// instead of one prompt each.
/// </summary>
public static class ElevatedRelauncher
{
    private const int Win32ErrorCancelled = 1223;

    /// <summary>
    /// Starts an elevated copy and closes this one. Returns false (leaving
    /// the app running) if the user dismissed the UAC prompt or Windows
    /// refused to start the new process.
    /// </summary>
    public static bool TryRelaunchElevated(IWin32Window owner)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                // Environment.ProcessPath is the real host exe, which is what
                // a single-file published app needs (the entry assembly's
                // location is empty there).
                FileName = Environment.ProcessPath ?? Application.ExecutablePath,
                UseShellExecute = true,
                Verb = "runas",
            });
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == Win32ErrorCancelled)
        {
            return false;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(owner, $"No se pudo reiniciar como administrador: {ex.Message}",
                "Error al elevar", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        Application.Exit();
        return true;
    }
}
