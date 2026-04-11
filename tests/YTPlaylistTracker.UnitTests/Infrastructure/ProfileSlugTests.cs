using YTPlaylistTracker.Infrastructure.YouTube;

namespace YTPlaylistTracker.UnitTests.Infrastructure;

public class ProfileSlugTests
{
    [Test]
    [Arguments("Default", "default")]
    [Arguments("My Work Account", "my_work_account")]
    [Arguments("  spaces  ", "spaces")]
    [Arguments("caf\u00e9", "caf_")]
    [Arguments("user@name!", "user_name_")]
    [Arguments("UPPER-case_123", "upper-case_123")]
    [Arguments("", "default")]
    [Arguments("   ", "default")]
    public async Task ToProfileSlug_NormalizesCorrectly(string input, string expected)
    {
        await Assert.That(YouTubeApiServiceFactory.ToProfileSlug(input)).IsEqualTo(expected);
    }
}
