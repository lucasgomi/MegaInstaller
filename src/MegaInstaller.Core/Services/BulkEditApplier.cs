using MegaInstaller.Core.Models;

namespace MegaInstaller.Core.Services;

/// <summary>Applies a <see cref="BulkEditOptions"/> to several entries in place.</summary>
public static class BulkEditApplier
{
    public static void Apply(IEnumerable<InstallerEntry> entries, BulkEditOptions options)
    {
        foreach (var entry in entries)
        {
            if (options.Arguments is not null)
            {
                entry.Arguments = options.Arguments;
            }

            if (options.InstallDir is not null)
            {
                entry.Arguments = SilentArgsCatalog.AppendInstallDir(entry.Arguments, entry.Type, options.InstallDir);
                entry.TargetInstallDir = options.InstallDir;
            }

            if (options.RunAsAdmin is not null)
            {
                entry.RunAsAdmin = options.RunAsAdmin.Value;
            }

            if (options.Order is not null)
            {
                entry.Order = options.Order.Value;
            }

            if (!string.IsNullOrEmpty(options.AddTagsText))
            {
                entry.Tags = TagUtils.Add(entry.Tags, options.AddTagsText);
            }
        }
    }
}
