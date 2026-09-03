using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

public class InstallSchedulerTests
{
    [Fact]
    public void GroupIntoWaves_DistinctOrders_OneEntryPerWaveInAscendingOrder()
    {
        var entries = new[]
        {
            new InstallerEntry { Id = "a", Order = 30 },
            new InstallerEntry { Id = "b", Order = 10 },
            new InstallerEntry { Id = "c", Order = 20 },
        };

        var waves = InstallScheduler.GroupIntoWaves(entries);

        Assert.Equal(3, waves.Count);
        Assert.Equal("b", waves[0].Single().Id);
        Assert.Equal("c", waves[1].Single().Id);
        Assert.Equal("a", waves[2].Single().Id);
    }

    [Fact]
    public void GroupIntoWaves_SharedOrder_GroupedIntoSameWave()
    {
        var entries = new[]
        {
            new InstallerEntry { Id = "a", Order = 10 },
            new InstallerEntry { Id = "b", Order = 10 },
            new InstallerEntry { Id = "c", Order = 20 },
        };

        var waves = InstallScheduler.GroupIntoWaves(entries);

        Assert.Equal(2, waves.Count);
        Assert.Equal(new[] { "a", "b" }, waves[0].Select(e => e.Id));
        Assert.Equal(new[] { "c" }, waves[1].Select(e => e.Id));
    }

    [Fact]
    public void GroupIntoWaves_Empty_ReturnsNoWaves()
    {
        var waves = InstallScheduler.GroupIntoWaves(Array.Empty<InstallerEntry>());

        Assert.Empty(waves);
    }
}
