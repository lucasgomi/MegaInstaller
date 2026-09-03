using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

public class InstanceInstallPlannerTests
{
    [Fact]
    public void BuildPlan_EasyMode_IncludesEverythingUnchangedInOrder()
    {
        var entries = new[]
        {
            new InstallerEntry { Id = "a", Name = "A", Order = 20, Arguments = "/S" },
            new InstallerEntry { Id = "b", Name = "B", Order = 10, Arguments = "/S" },
        };

        var plan = InstanceInstallPlanner.BuildPlan(entries);

        Assert.Equal(new[] { "b", "a" }, plan.Select(e => e.Id));
        Assert.Same(entries[1], plan[0]);
    }

    [Fact]
    public void BuildPlan_ExcludesGivenIds()
    {
        var entries = new[]
        {
            new InstallerEntry { Id = "a", Name = "A" },
            new InstallerEntry { Id = "b", Name = "B" },
        };

        var plan = InstanceInstallPlanner.BuildPlan(entries, excludedIds: new HashSet<string> { "a" });

        Assert.Equal(new[] { "b" }, plan.Select(e => e.Id));
    }

    [Fact]
    public void BuildPlan_OverrideDir_AppliedToSupportedType_ProducesClone()
    {
        var entry = new InstallerEntry { Id = "a", Name = "A", Type = InstallerType.Nsis, Arguments = "/S" };

        var plan = InstanceInstallPlanner.BuildPlan(new[] { entry }, overrideInstallDir: @"C:\Custom");

        var planned = Assert.Single(plan);
        Assert.NotSame(entry, planned);
        Assert.Equal(@"/S /D=C:\Custom", planned.Arguments);
        Assert.Equal(@"C:\Custom", planned.TargetInstallDir);
        Assert.Equal("/S", entry.Arguments); // original untouched
    }

    [Fact]
    public void BuildPlan_OverrideDir_UnsupportedType_LeavesEntryUnchanged()
    {
        var entry = new InstallerEntry { Id = "a", Name = "A", Type = InstallerType.InstallShield, Arguments = "/s" };

        var plan = InstanceInstallPlanner.BuildPlan(new[] { entry }, overrideInstallDir: @"C:\Custom");

        Assert.Same(entry, plan[0]);
    }

    [Fact]
    public void BuildPlan_NoOverrideDir_ReturnsOriginalInstances()
    {
        var entry = new InstallerEntry { Id = "a", Name = "A", Type = InstallerType.Msi };

        var plan = InstanceInstallPlanner.BuildPlan(new[] { entry });

        Assert.Same(entry, plan[0]);
    }
}
