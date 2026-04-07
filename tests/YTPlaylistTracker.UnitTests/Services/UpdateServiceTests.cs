using YTPlaylistTracker.Infrastructure.Update;

namespace YTPlaylistTracker.UnitTests.Services;

public class UpdateServiceTests
{
    [Fact]
    public void GetCurrentVersion_strips_build_metadata()
    {
        var version = UpdateService.GetCurrentVersion();
        Assert.DoesNotContain("+", version);
        Assert.True(Version.TryParse(version, out _), $"'{version}' is not a valid System.Version");
    }

    [Theory]
    [InlineData("0.5.0", "0.6.0", true)]
    [InlineData("0.6.0", "0.6.0", false)]
    [InlineData("0.7.0", "0.6.0", false)]
    [InlineData("1.0.0", "0.9.0", false)]
    [InlineData("0.1.0", "1.0.0", true)]
    public void Version_comparison_detects_newer_correctly(string current, string latest, bool expected)
    {
        var currentVer = Version.Parse(current);
        var latestVer = Version.Parse(latest);
        Assert.Equal(expected, latestVer > currentVer);
    }

    [Fact]
    public void UpdateException_preserves_manual_download_url()
    {
        var ex = new UpdateException("test error", "https://example.com/download");
        Assert.Equal("test error", ex.Message);
        Assert.Equal("https://example.com/download", ex.ManualDownloadUrl);
    }

    [Fact]
    public void UpdateException_without_url_has_null_url()
    {
        var ex = new UpdateException("test error");
        Assert.Null(ex.ManualDownloadUrl);
    }
}
