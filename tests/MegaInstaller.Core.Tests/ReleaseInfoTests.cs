using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

public class ReleaseInfoTests
{
    [Fact]
    public void IsNewer_HigherNumber_ReturnsTrue()
    {
        // Regression guard: "v9" > "v15" as plain strings, but 9 < 15 as
        // the version numbers they actually represent - a naive string
        // compare would get this backwards.
        Assert.True(ReleaseInfo.IsNewer("v9", "v15"));
    }

    [Fact]
    public void IsNewer_SameVersion_ReturnsFalse()
    {
        Assert.False(ReleaseInfo.IsNewer("v16", "v16"));
    }

    [Fact]
    public void IsNewer_LowerNumber_ReturnsFalse()
    {
        Assert.False(ReleaseInfo.IsNewer("v16", "v15"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-version")]
    [InlineData("v")]
    public void IsNewer_MalformedLatestTag_ReturnsFalse(string? malformed)
    {
        Assert.False(ReleaseInfo.IsNewer("v10", malformed));
    }

    [Fact]
    public void IsNewer_CaseInsensitivePrefix()
    {
        Assert.True(ReleaseInfo.IsNewer("v9", "V15"));
    }

    [Fact]
    public void IsNewer_MalformedCurrentVersion_ReturnsFalse()
    {
        Assert.False(ReleaseInfo.IsNewer("not-a-version", "v15"));
    }

    [Fact]
    public void IsNewer_AgainstLiveCurrentVersion_NeverThrows()
    {
        // A smoke test against the real constant, so a future malformed
        // CurrentVersion (e.g. someone forgets the "v" prefix when bumping
        // it) fails loudly here instead of silently breaking the update checker.
        Assert.False(ReleaseInfo.IsNewer(ReleaseInfo.CurrentVersion));

        var currentNumber = int.Parse(ReleaseInfo.CurrentVersion.TrimStart('v', 'V'));
        Assert.True(ReleaseInfo.IsNewer($"v{currentNumber + 1}"));
    }
}
