using System.Diagnostics;
using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.App;

/// <summary>
/// Downloads a newer MegaInstaller.exe and swaps it in for the running one.
/// Windows won't let a process overwrite or delete its own running
/// executable, so this drops a small PowerShell script that waits for this
/// process to exit, copies the new exe over the current path, relaunches
/// it, and cleans up after itself.
/// </summary>
public static class AppUpdater
{
    /// <summary>
    /// Downloads the update and launches the swap-and-relaunch script.
    /// Returns true once that script is running - the caller should exit
    /// right away rather than keep using the window, since the old exe is
    /// about to be replaced out from under it. Any failure (download,
    /// missing exe path) is reported to the user here and returns false.
    /// </summary>
    public static async Task<bool> DownloadAndInstallAsync(
        IWin32Window owner,
        string exeDownloadUrl,
        IProgress<DownloadProgressInfo>? progress,
        CancellationToken cancellationToken)
    {
        var currentExePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(currentExePath))
        {
            MessageBox.Show(owner, "No se pudo determinar la ubicación del ejecutable actual.", "No se pudo actualizar",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        var tempDir = Directory.CreateTempSubdirectory("megainstaller-update-").FullName;
        var newExePath = Path.Combine(tempDir, "MegaInstaller.new.exe");

        try
        {
            using var downloadService = new DownloadService();
            await downloadService.DownloadAsync(new Uri(exeDownloadUrl), newExePath, progress, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            TryDeleteDirectory(tempDir);
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            MessageBox.Show(owner, $"No se pudo descargar la actualización: {ex.Message}", "No se pudo actualizar",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            TryDeleteDirectory(tempDir);
            return false;
        }

        var downloaded = new FileInfo(newExePath);
        if (!downloaded.Exists || downloaded.Length == 0)
        {
            MessageBox.Show(owner, "La descarga no produjo un archivo válido.", "No se pudo actualizar",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            TryDeleteDirectory(tempDir);
            return false;
        }

        var scriptPath = Path.Combine(tempDir, "apply-update.ps1");
        try
        {
            File.WriteAllText(scriptPath, BuildUpdateScript(Environment.ProcessId, newExePath, currentExePath));

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });

            return true;
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(owner, $"No se pudo iniciar la actualización: {ex.Message}", "No se pudo actualizar",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            TryDeleteDirectory(tempDir);
            return false;
        }
    }

    private static string BuildUpdateScript(int currentProcessId, string newExePath, string currentExePath)
    {
        // Single-quoted PowerShell strings, so embedded single quotes must be
        // doubled (the PowerShell escaping convention) to avoid breaking out.
        static string Escape(string path) => path.Replace("'", "''");

        return $$"""
            $ErrorActionPreference = 'SilentlyContinue'
            try { Wait-Process -Id {{currentProcessId}} -Timeout 30 } catch {}
            $copied = $false
            for ($i = 0; $i -lt 10 -and -not $copied; $i++) {
                Start-Sleep -Milliseconds 300
                Copy-Item -Path '{{Escape(newExePath)}}' -Destination '{{Escape(currentExePath)}}' -Force
                if ($?) { $copied = $true }
            }
            Start-Process -FilePath '{{Escape(currentExePath)}}'
            Remove-Item -Path '{{Escape(newExePath)}}' -Force
            Remove-Item -Path $MyInvocation.MyCommand.Path -Force
            """;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
