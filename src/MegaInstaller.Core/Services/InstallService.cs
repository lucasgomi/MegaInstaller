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
/// speed.
///
/// Elevation has two modes. When MegaInstaller itself is not elevated, a
/// RunAsAdmin entry goes through ShellExecute with the "runas" verb - the
/// only way a non-admin process can raise a UAC prompt - and those are run
/// one at a time so prompts never stack, with no console redirection
/// (Windows forbids it for "runas"). When MegaInstaller is already
/// elevated, children inherit its token: no prompt at all, output stays
/// readable, and admin installs are free to run concurrently like the rest.
/// </summary>
public sealed class InstallService
{
    private const int MsiSuccessRebootRequired = 3010;
    private const int MsiSuccessRebootInitiated = 1641;
    private const int Win32ErrorCancelled = 1223;
    private const int MaxConcurrentInstalls = 4;

    private readonly bool _runningElevated;

    /// <param name="runningElevated">Overrides the elevation probe; for tests.</param>
    public InstallService(bool? runningElevated = null)
    {
        _runningElevated = runningElevated ?? ElevationProbe.IsProcessElevated();
    }

    public event EventHandler<InstallLogEventArgs>? Log;

    public async Task<IReadOnlyList<InstallResult>> InstallBatchAsync(
        string folder,
        IEnumerable<InstallerEntry> entries,
        bool stopOnError,
        IProgress<InstallProgress>? progress,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? entryFolderOverrides = null)
    {
        var waves = InstallScheduler.GroupIntoWaves(entries);
        var total = waves.Sum(w => w.Count);
        var results = new List<InstallResult>();
        var completedCounter = new CompletedCounter();

        RaiseLog(_runningElevated
            ? "MegaInstaller se está ejecutando como administrador: los instaladores heredan la elevación y no habrá más avisos de UAC."
            : "MegaInstaller no está elevado: cada programa marcado como administrador pedirá su propio UAC, de uno en uno.");

        foreach (var wave in waves)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var adminGate = new SemaphoreSlim(1, 1);
            using var concurrencyGate = new SemaphoreSlim(MaxConcurrentInstalls, MaxConcurrentInstalls);

            // Only serialize the entries that will actually raise a prompt.
            // Once elevation is inherited there is nothing to stack, so they
            // go through the regular concurrency gate and the batch is faster.
            var waveTasks = wave.Select(entry => RunGatedAsync(
                folder, entry, NeedsRunAsVerb(entry) ? adminGate : concurrencyGate,
                completedCounter, total, progress, cancellationToken, entryFolderOverrides));

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
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? entryFolderOverrides)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            progress?.Report(new InstallProgress { Completed = completedCounter.Value, Total = total, Current = entry });

            var result = await InstallOneAsync(folder, entry, cancellationToken, entryFolderOverrides).ConfigureAwait(false);

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

    public async Task<InstallResult> InstallOneAsync(
        string folder,
        InstallerEntry entry,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? entryFolderOverrides = null)
    {
        // A web-sourced entry (see InstallerEntry.MirrorUrl) has no file
        // under `folder` at all - the caller downloads it into a cache
        // folder first and points us at that instead, keyed by entry.Id.
        var effectiveFolder = entryFolderOverrides is not null && entryFolderOverrides.TryGetValue(entry.Id, out var overrideFolder)
            ? overrideFolder
            : folder;

        var fullPath = Path.Combine(effectiveFolder, entry.FileName);
        if (!File.Exists(fullPath))
        {
            RaiseLog($"[{entry.Name}] Archivo no encontrado: {fullPath}");
            return Fail(entry, InstallOutcome.FileNotFound, null, "Archivo no encontrado.");
        }

        var (fileName, arguments) = InstallCommandBuilder.Build(effectiveFolder, entry);
        var needsRunAs = NeedsRunAsVerb(entry);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = needsRunAs,
            CreateNoWindow = !needsRunAs,
            RedirectStandardOutput = !needsRunAs,
            RedirectStandardError = !needsRunAs,
        };

        if (needsRunAs)
        {
            startInfo.Verb = "runas";
        }

        RaiseLog($"[{entry.Name}] Iniciando: {fileName} {arguments}");
        if (needsRunAs)
        {
            RaiseLog($"[{entry.Name}] Se solicitará elevación (UAC). La salida de la consola no está disponible para procesos elevados.");
        }
        else if (entry.RunAsAdmin)
        {
            RaiseLog($"[{entry.Name}] Se ejecuta elevado heredando los permisos de MegaInstaller (sin aviso de UAC).");
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        if (!needsRunAs)
        {
            process.OutputDataReceived += (_, e) => { if (e.Data is not null) RaiseLog($"[{entry.Name}] {e.Data}"); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is not null) RaiseLog($"[{entry.Name}] {e.Data}"); };
        }

        try
        {
            process.Start();

            if (!needsRunAs)
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

    /// <summary>An entry only needs the UAC-raising "runas" verb when it wants admin and we don't already have it.</summary>
    private bool NeedsRunAsVerb(InstallerEntry entry) => entry.RunAsAdmin && !_runningElevated;

    private void RaiseLog(string message) => Log?.Invoke(this, new InstallLogEventArgs(message));

    private static InstallResult Fail(InstallerEntry entry, InstallOutcome outcome, int? exitCode, string message) =>
        new() { EntryId = entry.Id, Name = entry.Name, Outcome = outcome, ExitCode = exitCode, Message = message };
}
