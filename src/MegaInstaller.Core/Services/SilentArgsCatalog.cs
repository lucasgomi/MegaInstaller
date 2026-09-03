using MegaInstaller.Core.Models;

namespace MegaInstaller.Core.Services;

/// <summary>
/// Known silent-install switches and install-directory flags per installer
/// family. These are only ever used as a starting suggestion that gets
/// written into an editable text field - never applied invisibly - so a
/// wrong guess can always be fixed before anything runs.
/// </summary>
public static class SilentArgsCatalog
{
    public static string GetSuggestedArguments(InstallerType type) => type switch
    {
        InstallerType.Msi => "/qn /norestart",
        InstallerType.Nsis => "/S",
        InstallerType.InnoSetup => "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-",
        InstallerType.InstallShield => "/s /v\"/qn /norestart\"",
        _ => string.Empty
    };

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
            // NSIS requires /D=... to be the last argument and unquoted, even with spaces.
            InstallerType.Nsis => Join(existingArguments, $"/D={installDir}"),
            _ => existingArguments
        };
    }

    private static string Join(string arguments, string extra) =>
        string.IsNullOrWhiteSpace(arguments) ? extra : $"{arguments} {extra}";
}
