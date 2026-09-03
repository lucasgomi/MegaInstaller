using MegaInstaller.Core.Models;

namespace MegaInstaller.Core.Services;

/// <summary>
/// Builds the effective, per-run list of entries to install for an
/// instance: "easy mode" is just this with no exclusions and no override;
/// "advanced mode" excludes whatever the user unchecked and can redirect
/// supported installers to a custom folder for this run only, without
/// touching the stored entries.
/// </summary>
public static class InstanceInstallPlanner
{
    public static List<InstallerEntry> BuildPlan(
        IEnumerable<InstallerEntry> entries,
        IReadOnlySet<string>? excludedIds = null,
        string? overrideInstallDir = null)
    {
        var excluded = excludedIds ?? new HashSet<string>();
        var plan = new List<InstallerEntry>();

        foreach (var entry in entries)
        {
            if (excluded.Contains(entry.Id))
            {
                continue;
            }

            plan.Add(WithOverrideInstallDir(entry, overrideInstallDir));
        }

        return plan.OrderBy(e => e.Order).ToList();
    }

    /// <summary>
    /// Returns a copy of <paramref name="entry"/> with the install-dir flag
    /// applied to its arguments, or the original entry unchanged if no
    /// override was given or the installer type has no reliable universal
    /// flag for it (see <see cref="SilentArgsCatalog.AppendInstallDir"/>).
    /// </summary>
    public static InstallerEntry WithOverrideInstallDir(InstallerEntry entry, string? overrideInstallDir)
    {
        if (string.IsNullOrWhiteSpace(overrideInstallDir))
        {
            return entry;
        }

        var newArguments = SilentArgsCatalog.AppendInstallDir(entry.Arguments, entry.Type, overrideInstallDir);
        if (newArguments == entry.Arguments)
        {
            // Type doesn't support a reliable install-dir flag; leave untouched.
            return entry;
        }

        return new InstallerEntry
        {
            Id = entry.Id,
            Name = entry.Name,
            FileName = entry.FileName,
            SourceUrl = entry.SourceUrl,
            Type = entry.Type,
            Arguments = newArguments,
            TargetInstallDir = overrideInstallDir,
            RunAsAdmin = entry.RunAsAdmin,
            Enabled = entry.Enabled,
            Order = entry.Order,
            Notes = entry.Notes,
            AddedUtc = entry.AddedUtc,
        };
    }
}
