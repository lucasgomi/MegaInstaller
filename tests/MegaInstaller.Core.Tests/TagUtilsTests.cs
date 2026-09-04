using MegaInstaller.Core.Services;

namespace MegaInstaller.Core.Tests;

public class TagUtilsTests
{
    [Fact]
    public void Parse_SplitsTrimsAndDropsEmpties()
    {
        var tags = TagUtils.Parse(" dev ,, media,cli , ");

        Assert.Equal(new[] { "dev", "media", "cli" }, tags);
    }

    [Fact]
    public void Parse_RemovesCaseInsensitiveDuplicates_KeepingFirstSeenCasing()
    {
        var tags = TagUtils.Parse("Dev, dev, DEV, Tools");

        Assert.Equal(new[] { "Dev", "Tools" }, tags);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        Assert.Empty(TagUtils.Parse(""));
        Assert.Empty(TagUtils.Parse("   "));
    }

    [Fact]
    public void Join_ProducesCommaSpaceSeparatedText()
    {
        Assert.Equal("dev, media", TagUtils.Join(new[] { "dev", "media" }));
    }

    [Fact]
    public void Add_UnionsExistingAndNewTags_Deduplicated()
    {
        var result = TagUtils.Add(new[] { "dev" }, "dev, media, Dev");

        Assert.Equal(new[] { "dev", "media" }, result);
    }

    [Fact]
    public void MatchesAny_IsCaseInsensitiveSubstringMatch()
    {
        Assert.True(TagUtils.MatchesAny(new[] { "Developer Tools" }, "dev"));
        Assert.False(TagUtils.MatchesAny(new[] { "media" }, "dev"));
    }
}
