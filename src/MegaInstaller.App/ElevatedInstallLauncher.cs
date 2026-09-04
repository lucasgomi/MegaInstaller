using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using MegaInstaller.Core.Models;

namespace MegaInstaller.App;

/// <summary>
/// Runs a whole batch in an elevated copy of MegaInstaller, which costs a
/// single UAC prompt because every installer it launches inherits that
/// token. The current window keeps running - unlike restarting the app,
/// nothing is lost and the install doesn't have to be set up again.
/// </summary>
public static class ElevatedInstallLauncher
{
    private const int Win32ErrorCancelled = 1223;

    /// <summary>Command-line switch the elevated copy is started with, followed by the plan file path.</summary>
    public const string BatchSwitch = "--elevated-batch";

    /// <summary>
    /// Hands the batch to an elevated instance. Returns false if the user
    /// dismissed the UAC prompt or the plan could not be handed over, in
    /// which case the caller should just install normally.
    /// </summary>
    public static bool TryLaunch(IWin32Window owner, string folder, IEnumerable<InstallerEntry> entries, bool stopOnError)
    {
        var plan = new PendingInstallPlan
        {
            Folder = folder,
            StopOnError = stopOnError,
            Entries = entries.ToList(),
        };

        string planPath;
        try
        {
            planPath = WritePlan(plan);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(owner, $"No se pudo preparar la instalación elevada: {ex.Message}",
                "Error al elevar", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                // Environment.ProcessPath is the real host exe, which is what
                // a single-file published app needs.
                FileName = Environment.ProcessPath ?? Application.ExecutablePath,
                Arguments = $"{BatchSwitch} \"{planPath}\"",
                UseShellExecute = true,
                Verb = "runas",
            });

            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == Win32ErrorCancelled)
        {
            TryDelete(planPath);
            return false;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            TryDelete(planPath);
            MessageBox.Show(owner, $"No se pudo abrir la ventana elevada: {ex.Message}",
                "Error al elevar", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    /// <summary>Reads (and removes) a plan handed over by the non-elevated instance.</summary>
    public static PendingInstallPlan? ConsumePlan(string planPath)
    {
        try
        {
            var json = File.ReadAllText(planPath);
            return JsonSerializer.Deserialize<PendingInstallPlan>(json);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
        finally
        {
            TryDelete(planPath);
        }
    }

    private static string WritePlan(PendingInstallPlan plan)
    {
        // Kept under the user's own profile rather than the shared temp
        // directory: this file decides what an elevated process will run, so
        // it lives somewhere other standard users cannot write to.
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MegaInstaller");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"pending-install-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(plan));
        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leftover plan files are harmless; they are per-launch and tiny.
        }
    }
}
