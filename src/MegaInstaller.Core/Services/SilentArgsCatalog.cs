using MegaInstaller.Core.Models;

namespace MegaInstaller.Core.Services;

/// <summary>
/// Known silent-install switches, install-directory flags and logging flags
/// per installer family. These are only ever used as a starting suggestion
/// that gets written into an editable text field - never applied invisibly -
/// so a wrong guess can always be fixed before anything runs.
/// </summary>
public static class SilentArgsCatalog
{
    public static string GetSuggestedArguments(InstallerType type) => type switch
    {
        InstallerType.Msi => "/qn /norestart",
        InstallerType.Nsis => "/S",
        InstallerType.InnoSetup => "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-",
        InstallerType.InstallShield => "/s /v\"/qn /norestart\"",
        InstallerType.WixBurn => "/quiet /norestart",
        InstallerType.Squirrel => "--silent",
        InstallerType.SevenZipSfx => "-y",
        InstallerType.Wise => "/s",
        // Msix/Msu don't take flags directly - InstallCommandBuilder wraps
        // them in Add-AppxPackage / wusa.exe, which carry their own.
        _ => string.Empty
    };

    /// <summary>Whether <see cref="AppendInstallDir"/> knows a reliable flag for this family.</summary>
    public static bool SupportsInstallDir(InstallerType type) =>
        type is InstallerType.Msi or InstallerType.InnoSetup or InstallerType.Nsis
            or InstallerType.WixBurn or InstallerType.SevenZipSfx;

    /// <summary>
    /// Appends the install-directory flag for the given installer family to
    /// an existing argument string. Returns the arguments unchanged for
    /// families with no reliable, universal flag (e.g. InstallShield, whose
    /// property name varies per installer) - those need a manually-typed flag.
    /// </summary>
    public static string AppendInstallDir(string existingArguments, InstallerType type, string installDir)
    {
        if (string.IsNullOrWhiteSpace(installDir))
        {
            return existingArguments;
        }

        return type switch
        {
            InstallerType.Msi => Join(existingArguments, $"INSTALLDIR=\"{installDir}\""),
            InstallerType.InnoSetup => Join(existingArguments, $"/DIR=\"{installDir}\""),
            // A Burn bundle forwards unknown NAME=VALUE pairs to the MSIs it wraps.
            InstallerType.WixBurn => Join(existingArguments, $"INSTALLFOLDER=\"{installDir}\""),
            InstallerType.SevenZipSfx => Join(existingArguments, $"-o\"{installDir}\""),
            // NSIS requires /D=... to be the last argument and unquoted, even with spaces.
            InstallerType.Nsis => Join(existingArguments, $"/D={installDir}"),
            _ => existingArguments
        };
    }

    /// <summary>Whether this family has a known "don't reboot afterwards" switch.</summary>
    public static bool SupportsNoRestart(InstallerType type) => GetNoRestartArgument(type) is not null;

    public static string? GetNoRestartArgument(InstallerType type) => type switch
    {
        InstallerType.Msi or InstallerType.WixBurn => "/norestart",
        InstallerType.InnoSetup => "/NORESTART",
        InstallerType.InstallShield => "/v\"/norestart\"",
        _ => null
    };

    /// <summary>Appends the family's no-restart switch, if it has one.</summary>
    public static string AppendNoRestart(string existingArguments, InstallerType type)
    {
        var flag = GetNoRestartArgument(type);
        if (flag is null || ContainsFlag(existingArguments, flag))
        {
            return existingArguments;
        }

        return Join(existingArguments, flag);
    }

    /// <summary>Whether this family can write an install log to a path we choose.</summary>
    public static bool SupportsLogging(InstallerType type) =>
        type is InstallerType.Msi or InstallerType.InnoSetup or InstallerType.WixBurn;

    /// <summary>
    /// Appends the family's "write a verbose log here" flag. A log is what
    /// makes a failed silent install diagnosable at all, so the
    /// troubleshooter offers this as its first retry step.
    /// </summary>
    public static string AppendLogging(string existingArguments, InstallerType type, string logPath)
    {
        if (string.IsNullOrWhiteSpace(logPath))
        {
            return existingArguments;
        }

        return type switch
        {
            InstallerType.Msi => Join(existingArguments, $"/l*v \"{logPath}\""),
            InstallerType.InnoSetup => Join(existingArguments, $"/LOG=\"{logPath}\""),
            InstallerType.WixBurn => Join(existingArguments, $"/log \"{logPath}\""),
            _ => existingArguments
        };
    }

    /// <summary>
    /// Ordered alternative argument sets to try when the configured ones
    /// failed, most likely first. For an unknown family this walks the
    /// common silent conventions, which is how the troubleshooter can find
    /// working flags for an installer nobody has identified yet.
    /// </summary>
    public static IReadOnlyList<string> GetAlternativeArguments(InstallerType type) => type switch
    {
        InstallerType.Msi => new[] { "/qn /norestart", "/qb! /norestart", "/passive /norestart" },
        InstallerType.Nsis => new[] { "/S", "/S /NCRC", "/silent" },
        InstallerType.InnoSetup => new[]
        {
            "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-",
            "/SILENT /SUPPRESSMSGBOXES /NORESTART /SP-",
            "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /NOICONS",
        },
        InstallerType.InstallShield => new[] { "/s /v\"/qn /norestart\"", "/s /v\"/qb! /norestart\"", "/silent" },
        InstallerType.WixBurn => new[] { "/quiet /norestart", "/passive /norestart", "/silent" },
        InstallerType.Squirrel => new[] { "--silent", "-s" },
        InstallerType.SevenZipSfx => new[] { "-y", "-y -gm2" },
        InstallerType.Wise => new[] { "/s", "/s /z" },
        // Nothing identified the installer, so try the conventions in
        // rough order of how common they are among Windows setups.
        _ => new[] { "/S", "/silent", "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART", "/qn /norestart", "/quiet", "-y" },
    };

    private static bool ContainsFlag(string arguments, string flag) =>
        arguments.Contains(flag, StringComparison.OrdinalIgnoreCase);

    private static string Join(string arguments, string extra) =>
        string.IsNullOrWhiteSpace(arguments) ? extra : $"{arguments} {extra}";
}
