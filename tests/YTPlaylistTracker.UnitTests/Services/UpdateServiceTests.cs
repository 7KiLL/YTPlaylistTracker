using YTPlaylistTracker.Infrastructure.Update;

namespace YTPlaylistTracker.UnitTests.Services;

public class UpdateServiceTests
{
    [Test]
    public async Task GetCurrentVersion_strips_build_metadata()
    {
        var version = UpdateService.GetCurrentVersion();
        await Assert.That(version).DoesNotContain("+");
        await Assert.That(Version.TryParse(version, out _)).IsTrue();
    }

    [Test]
    [Arguments("0.5.0", "0.6.0", true)]
    [Arguments("0.6.0", "0.6.0", false)]
    [Arguments("0.7.0", "0.6.0", false)]
    [Arguments("1.0.0", "0.9.0", false)]
    [Arguments("0.1.0", "1.0.0", true)]
    public async Task Version_comparison_detects_newer_correctly(string current, string latest, bool expected)
    {
        var currentVer = Version.Parse(current);
        var latestVer = Version.Parse(latest);
        await Assert.That(latestVer > currentVer).IsEqualTo(expected);
    }

    [Test]
    public async Task UpdateException_preserves_manual_download_url()
    {
        var ex = new UpdateException("test error", "https://example.com/download");
        await Assert.That(ex.Message).IsEqualTo("test error");
        await Assert.That(ex.ManualDownloadUrl).IsEqualTo("https://example.com/download");
    }

    [Test]
    public async Task UpdateException_without_url_has_null_url()
    {
        var ex = new UpdateException("test error");
        await Assert.That(ex.ManualDownloadUrl).IsNull();
    }
}
