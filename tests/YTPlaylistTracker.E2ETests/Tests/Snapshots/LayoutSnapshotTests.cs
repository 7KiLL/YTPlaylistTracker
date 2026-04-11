using YTPlaylistTracker.E2ETests.Builders;
using YTPlaylistTracker.E2ETests.Harness;
using YTPlaylistTracker.Domain.Enums;

namespace YTPlaylistTracker.E2ETests.Tests.Snapshots;

[NotInParallel("TerminalGui")]
[Category("Snapshot")]
public class LayoutSnapshotTests
{
    private AppHarness? _harness;

    [After(Test)]
    public async Task Teardown()
    {
        if (_harness is not null)
            await _harness.DisposeAsync();
    }

    [Test]
    public async Task EmptyState_DefaultLayout()
    {
        _harness = await new AppHarnessBuilder().BuildAsync();
        await Verify(_harness.CaptureScreen());
    }

    [Test]
    public async Task WithPlaylistsAndVideos()
    {
        var profile = new ProfileBuilder().Build();
        var pl1 = new PlaylistBuilder().WithId(1).WithTitle("Favorites").Build(profile);
        var pl2 = new PlaylistBuilder().WithId(2).WithTitle("Music").Build(profile);
        // Use fixed dates so descending-date sort order is deterministic
        var v1 = new VideoBuilder().WithId(1).WithTitle("Never Gonna Give You Up").WithChannel("Rick Astley")
            .WithAddedAt(new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc)).Build(pl1);
        var v2 = new VideoBuilder().WithId(2).WithTitle("Bohemian Rhapsody").WithChannel("Queen")
            .WithAddedAt(new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc)).Build(pl1);
        var v3 = new VideoBuilder().WithId(3).WithTitle("Imagine").WithChannel("John Lennon")
            .WithAddedAt(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)).Build(pl1);

        _harness = await new AppHarnessBuilder()
            .WithProfile(profile)
            .WithPlaylist(profile, pl1)
            .WithPlaylist(profile, pl2)
            .WithVideo(pl1, v1)
            .WithVideo(pl1, v2)
            .WithVideo(pl1, v3)
            .BuildAsync();

        await Verify(_harness.CaptureScreen());
    }

    [Test]
    public async Task ProfilePaneHidden()
    {
        var profile = new ProfileBuilder().Build();
        var pl = new PlaylistBuilder().WithTitle("My Playlist").Build(profile);
        var v = new VideoBuilder().WithTitle("Test Video").WithChannel("Test Channel").Build(pl);

        _harness = await new AppHarnessBuilder()
            .WithProfile(profile)
            .WithPlaylist(profile, pl)
            .WithVideo(pl, v)
            .BuildAsync();

        // Profile pane auto-hides for single profile, but toggle it explicitly
        if (_harness.Screen.IsProfilePaneVisible)
            _harness.SendKey((Key)'p');

        await Verify(_harness.CaptureScreen());
    }

    [Test]
    public async Task WithDeletedVideos()
    {
        var profile = new ProfileBuilder().Build();
        var pl = new PlaylistBuilder().WithTitle("Favorites").Build(profile);
        var v1 = new VideoBuilder().WithId(1).WithTitle("Still Here").WithChannel("Artist").Build(pl);
        var v2 = new VideoBuilder().WithId(2).WithTitle("Gone Video").WithChannel("Artist").Deleted().Build(pl);
        var v3 = new VideoBuilder().WithId(3).WithTitle("Private Now").WithChannel("Artist").Deleted(RemovalReason.Private).Build(pl);

        _harness = await new AppHarnessBuilder()
            .WithProfile(profile)
            .WithPlaylist(profile, pl)
            .WithVideo(pl, v1)
            .WithVideo(pl, v2)
            .WithVideo(pl, v3)
            .BuildAsync();

        // Switch to deleted view
        _harness.SendKey(Key.F8);

        await Verify(_harness.CaptureScreen());
    }
}
