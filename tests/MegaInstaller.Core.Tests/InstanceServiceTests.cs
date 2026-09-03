using MegaInstaller.Core.Models;
using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

public class InstanceServiceTests
{
    [Fact]
    public void ResolveInstallers_ReturnsMembersInOrder_AndSkipsStaleIds()
    {
        var a = new InstallerEntry { Id = "a", Name = "A", Order = 20 };
        var b = new InstallerEntry { Id = "b", Name = "B", Order = 10 };
        var manifest = new InstallerManifest { Items = { a, b } };
        var instance = new InstanceDefinition { InstallerIds = { "a", "missing", "b" } };

        var resolved = InstanceService.ResolveInstallers(manifest, instance);

        Assert.Equal(new[] { "b", "a" }, resolved.Select(e => e.Id));
    }

    [Fact]
    public void SetMembership_True_AddsOnce()
    {
        var instance = new InstanceDefinition();

        InstanceService.SetMembership(instance, "x", true);
        InstanceService.SetMembership(instance, "x", true);

        Assert.Equal(new[] { "x" }, instance.InstallerIds);
    }

    [Fact]
    public void SetMembership_False_Removes()
    {
        var instance = new InstanceDefinition { InstallerIds = { "x", "y" } };

        InstanceService.SetMembership(instance, "x", false);

        Assert.Equal(new[] { "y" }, instance.InstallerIds);
    }

    [Fact]
    public void ApplyMembership_UpdatesEveryInstanceAccordingToGivenSet()
    {
        var pack1 = new InstanceDefinition { Id = "p1", InstallerIds = { "x" } };
        var pack2 = new InstanceDefinition { Id = "p2" };
        var manifest = new InstallerManifest { Instances = { pack1, pack2 } };

        InstanceService.ApplyMembership(manifest.Instances, "x", new HashSet<string> { "p2" });

        Assert.DoesNotContain("x", pack1.InstallerIds);
        Assert.Contains("x", pack2.InstallerIds);
    }
}
