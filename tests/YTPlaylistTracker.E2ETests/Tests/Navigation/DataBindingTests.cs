using YTPlaylistTracker.E2ETests.Builders;
using YTPlaylistTracker.E2ETests.Harness;

namespace YTPlaylistTracker.E2ETests.Tests.Navigation;

[NotInParallel("TerminalGui")]
[Category("DataBinding")]
public class DataBindingTests
{
    private AppHarness? _harness;

    [After(Test)]
    public async Task Teardown()
    {
        if (_harness is not null)
            await _harness.DisposeAsync();
    }

    [Test]
    public async Task J_Navigates_Down_InList()
    {
        var profile = new ProfileBuilder().Build();
        var pl1 = new PlaylistBuilder().WithId(1).WithTitle("First").Build(profile);
        var pl2 = new PlaylistBuilder().WithId(2).WithTitle("Second").Build(profile);
        var pl3 = new PlaylistBuilder().WithId(3).WithTitle("Third").Build(profile);

        _harness = await new AppHarnessBuilder()
            .WithProfile(profile)
            .WithPlaylist(profile, pl1)
            .WithPlaylist(profile, pl2)
            .WithPlaylist(profile, pl3)
            .BuildAsync();

        var before = _harness.Screen.SelectedPlaylistIndex;
        _harness.SendKey((Key)'j');
        await Assert.That(_harness.Screen.SelectedPlaylistIndex).IsNotEqualTo(before);
    }

    [Test]
    public async Task F8_Toggles_DeletedView()
    {
        var profile = new ProfileBuilder().Build();
        var pl = new PlaylistBuilder().Build(profile);

        _harness = await new AppHarnessBuilder()
            .WithProfile(profile)
            .WithPlaylist(profile, pl)
            .BuildAsync();

        await Assert.That(_harness.Screen.ShowDeletedOnly).IsFalse();
        _harness.SendKey(Key.F8);
        await Assert.That(_harness.Screen.ShowDeletedOnly).IsTrue();
    }

    [Test]
    public async Task Playlist_Select_Calls_GetVideos()
    {
        var profile = new ProfileBuilder().Build();
        var pl1 = new PlaylistBuilder().WithId(1).WithTitle("First").Build(profile);
        var pl2 = new PlaylistBuilder().WithId(2).WithTitle("Second").Build(profile);

        _harness = await new AppHarnessBuilder()
            .WithProfile(profile)
            .WithPlaylist(profile, pl1)
            .WithPlaylist(profile, pl2)
            .BuildAsync();

        _harness.SendKey((Key)'j');

        await _harness.Mocks.PlaylistRepo.Received().GetVideosAsync(pl2.Id);
    }
}
