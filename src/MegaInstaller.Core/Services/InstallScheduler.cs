using MegaInstaller.Core.Models;

namespace MegaInstaller.Core.Services;

/// <summary>
/// Groups installers into ordered "waves" for batch installation: waves run
/// strictly one after another (so a lower Order always finishes before a
/// higher one starts, preserving any dependency the user relies on), while
/// entries that share the same Order are considered independent and are
/// installed concurrently by <see cref="InstallService"/>. Giving two
/// installers the same Order is how you opt them into running together.
/// </summary>
public static class InstallScheduler
{
    public static IReadOnlyList<IReadOnlyList<InstallerEntry>> GroupIntoWaves(IEnumerable<InstallerEntry> entries) =>
        entries
            .GroupBy(e => e.Order)
            .OrderBy(g => g.Key)
            .Select(g => (IReadOnlyList<InstallerEntry>)g.ToList())
            .ToList();
}
