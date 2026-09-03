using MegaInstaller.Core.Models;

namespace MegaInstaller.Core.Services;

/// <summary>Resolves and edits instance ("pack") membership.</summary>
public static class InstanceService
{
    /// <summary>The entries belonging to an instance, in install order. Stale references (a deleted installer) are silently skipped.</summary>
    public static List<InstallerEntry> ResolveInstallers(InstallerManifest manifest, InstanceDefinition instance)
    {
        var byId = manifest.Items.ToDictionary(i => i.Id);
        return instance.InstallerIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .OrderBy(e => e.Order)
            .ToList();
    }

    public static void SetMembership(InstanceDefinition instance, string installerId, bool isMember)
    {
        if (isMember)
        {
            if (!instance.InstallerIds.Contains(installerId))
            {
                instance.InstallerIds.Add(installerId);
            }
        }
        else
        {
            instance.InstallerIds.Remove(installerId);
        }
    }

    /// <summary>Applies the given membership (checked/unchecked) for one installer across every one of the given instances.</summary>
    public static void ApplyMembership(IEnumerable<InstanceDefinition> instances, string installerId, IReadOnlySet<string> memberOfInstanceIds)
    {
        foreach (var instance in instances)
        {
            SetMembership(instance, installerId, memberOfInstanceIds.Contains(instance.Id));
        }
    }
}
