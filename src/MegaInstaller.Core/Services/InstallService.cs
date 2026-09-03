using System.ComponentModel;
using System.Diagnostics;
using MegaInstaller.Core.Models;

namespace MegaInstaller.Core.Services;

public sealed class InstallLogEventArgs : EventArgs
{
    public InstallLogEventArgs(string message) => Message = message;
    public string Message { get; }
}

public sealed class InstallProgress
{
    public required int Completed { get; init; }
    public required int Total { get; init; }
    public required InstallerEntry Current { get; init; }

    /// <summary>Set only on the "finished" report for <see cref="Current"/>; null while it's still running.</summary>
    public InstallResult? Result { get; init; }
}

/// <summary>
/// Runs installers in ordered "waves" (see <see cref="InstallScheduler"/>):
/// a wave fully finishes before the next one starts, but entries sharing
/// the same Order are considered independent and run concurrently for
/// speed - bounded, and with at most one elevated (RunAsAdmin) install in
/// flight at a time so UAC prompts never stack. Elevated installs go
/// through ShellExecute with the "runas" verb, which is the only way
/// Windows lets a non-admin process trigger a UAC prompt - Windows does
/// not allow redirecting stdout/stderr for a process launched that way, so
/// live console output is only available for non-elevated installs.
/// </summary>
public sealed class InstallService
{
    private const int MsiSuccessRebootRequired = 3010;
    private const int MsiSuccessRebootInitiated = 1641;
    private const int Win32ErrorCancelled = 1223;
    private const int MaxConcurrentInstalls = 4;

    public event EventHandler<InstallLogEventArgs>? Log;

    public async Task<IReadOnlyList<InstallResult>> InstallBatchAsync(
        string folder,
        IEnumerable<InstallerEntry> entries,
        bool stopOnError,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var waves = InstallScheduler.GroupIntoWaves(entries);
        var total = waves.Sum(w => w.Count);
        var results = new List<InstallResult>();
        var completedCounter = new CompletedCounter();

        foreach (var wave in waves)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var adminGate = new SemaphoreSlim(1, 1);
            using var concurrencyGate = new SemaphoreSlim(MaxConcurrentInstalls, MaxConcurrentInstalls);

            var waveTasks = wave.Select(entry => RunGatedAsync(
                folder, entry, entry.RunAsAdmin ? adminGate : concurrencyGate,
                completedCounter, total, progress, cancellationToken));

            var waveResults = await Task.WhenAll(waveTasks).ConfigureAwait(false);
            results.AddRange(waveResults);

            if (stopOnError && waveResults.Any(r => r.Outcome is InstallOutcome.Failed or InstallOutcome.FileNotFound))
            {
                break;
            }
        }

        return results;
    }

    private async Task<InstallResult> RunGatedAsync(
        string folder,
        InstallerEntry entry,
        SemaphoreSlim gate,
        CompletedCounter completedCounter,
        int total,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            progress?.Report(new InstallProgress { Completed = completedCounter.Value, Total = total, Current = entry });

            var result = await InstallOneAsync(folder, entry, cancellationToken).ConfigureAwait(false);

            progress?.Report(new InstallProgress { Completed = completedCounter.Increment(), Total = total, Current = entry, Result = result });

            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    private sealed class CompletedCounter
    {
        private int _value;
        public int Value => Volatile.Read(ref _value);
        public int Increment() => Interlocked.Increment(ref _value);
    }

    public async Task<InstallResult> InstallOneAsync(string folder, InstallerEntry entry, CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(folder, entry.FileName);
        if (!File.Exists(fullPath))
        {
            RaiseLog($"[{entry.Name}] Archivo no encontrado: {fullPath}");
            return Fail(entry, InstallOutcome.FileNotFound, null, "Archivo no encontrado.");
        }

        var (fileName, arguments) = InstallCommandBuilder.Build(folder, entry);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = entry.RunAsAdmin,
            CreateNoWindow = !entry.RunAsAdmin,
            RedirectStandardOutput = !entry.RunAsAdmin,
            RedirectStandardError = !entry.RunAsAdmin,
        };

        if (entry.RunAsAdmin)
        {
            startInfo.Verb = "runas";
        }

        RaiseLog($"[{entry.Name}] Iniciando: {fileName} {arguments}");
        if (entry.RunAsAdmin)
        {
            RaiseLog($"[{entry.Name}] Se solicitará elevación (UAC). La salida de la consola no está disponible para procesos elevados.");
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        if (!entry.RunAsAdmin)
        {
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) RaiseLog($"[{entry.Name}] {e.Data}"); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) RaiseLog($"[{entry.Name}] {e.Data}"); };
        }

        try
        {
            process.Start();

            if (!entry.RunAsAdmin)
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process, entry);
            RaiseLog($"[{entry.Name}] Cancelado.");
            return Fail(entry, InstallOutcome.Cancelled, null, "Cancelado por el usuario.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == Win32ErrorCancelled)
        {
            RaiseLog($"[{entry.Name}] El usuario rechazó la solicitud de permisos de administrador.");
            return Fail(entry, InstallOutcome.Cancelled, null, "Elevación (UAC) rechazada por el usuario.");
        }
        catch (Win32Exception ex)
        {
            RaiseLog($"[{entry.Name}] Error al iniciar el proceso: {ex.Message}");
            return Fail(entry, InstallOutcome.Failed, null, ex.Message);
        }

        var exitCode = process.ExitCode;
        RaiseLog($"[{entry.Name}] Código de salida: {exitCode}");

        var isMsi = string.Equals(Path.GetExtension(entry.FileName), ".msi", StringComparison.OrdinalIgnoreCase);
        if (exitCode == 0)
        {
            return new InstallResult { EntryId = entry.Id, Name = entry.Name, Outcome = InstallOutcome.Success, ExitCode = exitCode };
        }

        if (isMsi && (exitCode == MsiSuccessRebootRequired || exitCode == MsiSuccessRebootInitiated))
        {
            RaiseLog($"[{entry.Name}] Instalado correctamente; requiere reiniciar.");
            return new InstallResult { EntryId = entry.Id, Name = entry.Name, Outcome = InstallOutcome.SuccessRebootRequired, ExitCode = exitCode };
        }

        return Fail(entry, InstallOutcome.Failed, exitCode, $"Código de salida {exitCode}.");
    }

    private void TryKill(Process process, InstallerEntry entry)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            RaiseLog($"[{entry.Name}] No se pudo detener el proceso: {ex.Message}");
        }
    }

    private void RaiseLog(string message) => Log?.Invoke(this, new InstallLogEventArgs(message));

    private static InstallResult Fail(InstallerEntry entry, InstallOutcome outcome, int? exitCode, string message) =>
        new() { EntryId = entry.Id, Name = entry.Name, Outcome = outcome, ExitCode = exitCode, Message = message };
}
