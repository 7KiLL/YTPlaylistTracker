using YTPlaylistTracker.E2ETests.Builders;
using YTPlaylistTracker.E2ETests.Harness;
using YTPlaylistTracker.UI;

namespace YTPlaylistTracker.E2ETests.Tests.Smoke;

[NotInParallel("TerminalGui")]
public class AppBootTests
{
    private AppHarness? _harness;

    [After(Test)]
    public async Task Teardown()
    {
        if (_harness is not null)
            await _harness.DisposeAsync();
    }

    [Test]
    [Category("Smoke")]
    public async Task App_Init_InstanceApi_Works()
    {
        using IApplication app = TGuiApp.Create();
        app.Init(DriverRegistry.Names.ANSI);
        await Assert.That(app.Initialized).IsTrue();
    }

    [Test]
    [Category("Smoke")]
    public async Task Window_Initializes_WithEmptyData()
    {
        _harness = await new AppHarnessBuilder().BuildAsync();

        await Assert.That(_harness.Screen.Title).Contains("ytpt");
        await Assert.That(_harness.Screen.ProfileCount).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task Window_Loads_Playlists_And_Videos()
    {
        var profile = new ProfileBuilder().Build();
        var pl1 = new PlaylistBuilder().WithId(1).WithTitle("Favorites").Build(profile);
        var pl2 = new PlaylistBuilder().WithId(2).WithTitle("Music").Build(profile);
        var v1 = new VideoBuilder().WithId(1).WithTitle("Video A").Build(pl1);
        var v2 = new VideoBuilder().WithId(2).WithTitle("Video B").Build(pl1);
        var v3 = new VideoBuilder().WithId(3).WithTitle("Video C").Build(pl1);

        _harness = await new AppHarnessBuilder()
            .WithProfile(profile)
            .WithPlaylist(profile, pl1)
            .WithPlaylist(profile, pl2)
            .WithVideo(pl1, v1)
            .WithVideo(pl1, v2)
            .WithVideo(pl1, v3)
            .BuildAsync();

        await Assert.That(_harness.Screen.PlaylistCount).IsEqualTo(2);
        await Assert.That(_harness.Screen.VideoRowCount).IsEqualTo(3);
    }

    [Test]
    public async Task Theme_Applied_On_Boot()
    {
        _harness = await new AppHarnessBuilder()
            .WithTheme("Dracula")
            .BuildAsync();

        await Assert.That(Theme.CurrentName).IsEqualTo("Dracula");
    }
}
