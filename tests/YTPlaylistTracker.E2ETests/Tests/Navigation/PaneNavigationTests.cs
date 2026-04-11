using YTPlaylistTracker.E2ETests.Builders;
using YTPlaylistTracker.E2ETests.Harness;
using YTPlaylistTracker.E2ETests.Screens;

namespace YTPlaylistTracker.E2ETests.Tests.Navigation;

[NotInParallel("TerminalGui")]
[Category("Navigation")]
public class PaneNavigationTests
{
    private AppHarness? _harness;

    [After(Test)]
    public async Task Teardown()
    {
        if (_harness is not null)
            await _harness.DisposeAsync();
    }

    private async Task<AppHarness> BuildWithData()
    {
        var profile = new ProfileBuilder().Build();
        var pl = new PlaylistBuilder().WithTitle("Test PL").Build(profile);
        var v = new VideoBuilder().WithTitle("Test Video").Build(pl);
        return await new AppHarnessBuilder()
            .WithProfile(profile)
            .WithPlaylist(profile, pl)
            .WithVideo(pl, v)
            .BuildAsync();
    }

    [Test]
    public async Task Vim_L_Moves_Right()
    {
        _harness = await BuildWithData();
        _harness.SendKey((Key)'l');
        await Assert.That(_harness.Screen.FocusedPane).IsEqualTo(MainScreen.Pane.Video);
    }

    [Test]
    public async Task Vim_H_Moves_Left()
    {
        _harness = await BuildWithData();
        _harness.SendKey((Key)'l');
        _harness.SendKey((Key)'h');
        await Assert.That(_harness.Screen.FocusedPane).IsEqualTo(MainScreen.Pane.Playlist);
    }

    [Test]
    public async Task Tab_Cycles_Panes()
    {
        _harness = await BuildWithData();
        var before = _harness.Screen.FocusedPane;
        _harness.SendKey(Key.Tab);
        await Assert.That(_harness.Screen.FocusedPane).IsNotEqualTo(before);
    }

    [Test]
    public async Task P_Toggles_ProfilePane()
    {
        _harness = await BuildWithData();
        var before = _harness.Screen.IsProfilePaneVisible;
        _harness.SendKey((Key)'p');
        await Assert.That(_harness.Screen.IsProfilePaneVisible).IsNotEqualTo(before);
    }
}
