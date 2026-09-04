using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

public class BulkEditApplierTests
{
    [Fact]
    public void Apply_UncheckedFields_LeavesEntriesUnchanged()
    {
        var entry = new InstallerEntry { Arguments = "/S", RunAsAdmin = true, Order = 7, Tags = { "dev" } };

        BulkEditApplier.Apply(new[] { entry }, new BulkEditOptions());

        Assert.Equal("/S", entry.Arguments);
        Assert.True(entry.RunAsAdmin);
        Assert.Equal(7, entry.Order);
        Assert.Equal(new[] { "dev" }, entry.Tags);
        Assert.Null(entry.TargetInstallDir);
    }

    [Fact]
    public void Apply_Arguments_OverwritesForAllEntries()
    {
        var a = new InstallerEntry { Arguments = "/old" };
        var b = new InstallerEntry { Arguments = "" };

        BulkEditApplier.Apply(new[] { a, b }, new BulkEditOptions { Arguments = "/VERYSILENT" });

        Assert.Equal("/VERYSILENT", a.Arguments);
        Assert.Equal("/VERYSILENT", b.Arguments);
    }

    [Fact]
    public void Apply_InstallDir_AppendsPerEntryUsingItsOwnType()
    {
        var nsis = new InstallerEntry { Type = InstallerType.Nsis, Arguments = "/S" };
        var inno = new InstallerEntry { Type = InstallerType.InnoSetup, Arguments = "" };

        BulkEditApplier.Apply(new[] { nsis, inno }, new BulkEditOptions { InstallDir = @"C:\Apps" });

        Assert.Equal(@"/S /D=C:\Apps", nsis.Arguments);
        Assert.Equal("/DIR=\"C:\\Apps\"", inno.Arguments);
        Assert.Equal(@"C:\Apps", nsis.TargetInstallDir);
        Assert.Equal(@"C:\Apps", inno.TargetInstallDir);
    }

    [Fact]
    public void Apply_ArgumentsAndInstallDirTogether_OverwriteHappensBeforeAppend()
    {
        var entry = new InstallerEntry { Type = InstallerType.Nsis, Arguments = "/stale" };

        BulkEditApplier.Apply(new[] { entry }, new BulkEditOptions { Arguments = "/S", InstallDir = @"C:\Apps" });

        Assert.Equal(@"/S /D=C:\Apps", entry.Arguments);
    }

    [Fact]
    public void Apply_RunAsAdminAndOrder_SetForAllEntries()
    {
        var a = new InstallerEntry { RunAsAdmin = false, Order = 1 };
        var b = new InstallerEntry { RunAsAdmin = false, Order = 2 };

        BulkEditApplier.Apply(new[] { a, b }, new BulkEditOptions { RunAsAdmin = true, Order = 50 });

        Assert.True(a.RunAsAdmin);
        Assert.True(b.RunAsAdmin);
        Assert.Equal(50, a.Order);
        Assert.Equal(50, b.Order);
    }

    [Fact]
    public void Apply_AddTagsText_UnionsRatherThanOverwrites()
    {
        var entry = new InstallerEntry { Tags = { "dev" } };

        BulkEditApplier.Apply(new[] { entry }, new BulkEditOptions { AddTagsText = "dev, media" });

        Assert.Equal(new[] { "dev", "media" }, entry.Tags);
    }
}
