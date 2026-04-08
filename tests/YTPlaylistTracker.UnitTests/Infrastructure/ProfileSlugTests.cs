using YTPlaylistTracker.Infrastructure.YouTube;

namespace YTPlaylistTracker.UnitTests.Infrastructure;

public class ProfileSlugTests
{
    [Theory]
    [InlineData("Default", "default")]
    [InlineData("My Work Account", "my_work_account")]
    [InlineData("  spaces  ", "spaces")]
    [InlineData("café", "caf_")]
    [InlineData("user@name!", "user_name_")]
    [InlineData("UPPER-case_123", "upper-case_123")]
    [InlineData("", "default")]
    [InlineData("   ", "default")]
    public void ToProfileSlug_NormalizesCorrectly(string input, string expected)
    {
        Assert.Equal(expected, YouTubeApiServiceFactory.ToProfileSlug(input));
    }
}
