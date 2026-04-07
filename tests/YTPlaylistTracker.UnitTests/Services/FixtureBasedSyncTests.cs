using NSubstitute;
using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Application.Services;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Enums;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Domain.Models;
using YTPlaylistTracker.UnitTests.Helpers;

namespace YTPlaylistTracker.UnitTests.Services;

public class FixtureBasedSyncTests
{
    private readonly IYouTubeApiService _youtubeApi = Substitute.For<IYouTubeApiService>();
    private readonly IPlaylistRepository _playlistRepo = Substitute.For<IPlaylistRepository>();
    private readonly ISyncService _syncService;

    private const string TestPlaylistId = "PLtest_fixture_001";

    private readonly Playlist _testPlaylist = new()
    {
        Id = 1,
        ProfileId = 1,
        YouTubePlaylistId = TestPlaylistId,
        Title = "Test Fixture Playlist",
        IsTracked = true
    };

    public FixtureBasedSyncTests()
    {
        var logger = Substitute.For<ILogger<SyncService>>();
        _syncService = new SyncService(_youtubeApi, _playlistRepo, logger);
    }

    [Fact]
    public async Task FirstSync_AllVideosAdded()
    {
        var fixtureVideos = FixtureLoader.LoadPlaylistVideos(TestPlaylistId);
        _youtubeApi.GetPlaylistVideosAsync(TestPlaylistId).Returns(fixtureVideos);
        _playlistRepo.GetVideosAsync(1).Returns(new List<Video>());

        var result = await _syncService.SyncPlaylistAsync(_testPlaylist);

        Assert.Equal(fixtureVideos.Count, result.Added);
        Assert.Equal(0, result.Removed);
        Assert.Equal(0, result.Updated);
        await _playlistRepo.Received(1).AddVideosAsync(Arg.Is<IEnumerable<Video>>(v => v.Count() == fixtureVideos.Count));
    }

    [Fact]
    public async Task FirstSync_AddedAtPassedThrough()
    {
        var now = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var videos = new List<YouTubeVideoSnapshot>
        {
            new("vid1", "Test Video", "Channel", 0, now),
        };
        _youtubeApi.GetPlaylistVideosAsync(TestPlaylistId).Returns(videos);
        _playlistRepo.GetVideosAsync(1).Returns(new List<Video>());

        await _syncService.SyncPlaylistAsync(_testPlaylist);

        await _playlistRepo.Received(1).AddVideosAsync(Arg.Is<IEnumerable<Video>>(vs => vs.Any(v => v.AddedAt == now)));
    }

    [Fact]
    public async Task ReSync_SameData_NoChanges()
    {
        var fixtureVideos = FixtureLoader.LoadPlaylistVideos(TestPlaylistId);
        _youtubeApi.GetPlaylistVideosAsync(TestPlaylistId).Returns(fixtureVideos);

        var dbVideos = fixtureVideos.Select(v => new Video
        {
            Id = fixtureVideos.IndexOf(v) + 1,
            PlaylistId = 1,
            YouTubeVideoId = v.VideoId,
            Title = v.Title,
            ChannelTitle = v.ChannelTitle,
            Position = v.Position,
        }).ToList();
        _playlistRepo.GetVideosAsync(1).Returns(dbVideos);

        var result = await _syncService.SyncPlaylistAsync(_testPlaylist);

        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Removed);
        Assert.Equal(0, result.Updated);
    }

    [Fact]
    public async Task Sync_VideoRemoved_DetectedAsRemoval()
    {
        var fixtureVideos = FixtureLoader.LoadPlaylistVideos(TestPlaylistId);

        var dbVideos = fixtureVideos.Select((v, i) => new Video
        {
            Id = i + 1,
            PlaylistId = 1,
            YouTubeVideoId = v.VideoId,
            Title = v.Title,
            ChannelTitle = v.ChannelTitle,
            Position = v.Position,
        }).ToList();
        _playlistRepo.GetVideosAsync(1).Returns(dbVideos);

        var apiVideos = fixtureVideos.Skip(1).ToList();
        _youtubeApi.GetPlaylistVideosAsync(TestPlaylistId).Returns(apiVideos);
        _youtubeApi.CheckVideoStatusAsync(fixtureVideos[0].VideoId).Returns(RemovalReason.Deleted);

        var result = await _syncService.SyncPlaylistAsync(_testPlaylist);

        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.Removed);
        Assert.Equal(0, result.Updated);
        await _youtubeApi.Received(1).CheckVideoStatusAsync(fixtureVideos[0].VideoId);
    }

    [Fact]
    public async Task Sync_VideoAdded_DetectedAsAddition()
    {
        var fixtureVideos = FixtureLoader.LoadPlaylistVideos(TestPlaylistId);

        var dbVideos = fixtureVideos.Take(fixtureVideos.Count - 1).Select((v, i) => new Video
        {
            Id = i + 1,
            PlaylistId = 1,
            YouTubeVideoId = v.VideoId,
            Title = v.Title,
            ChannelTitle = v.ChannelTitle,
            Position = v.Position,
        }).ToList();
        _playlistRepo.GetVideosAsync(1).Returns(dbVideos);
        _youtubeApi.GetPlaylistVideosAsync(TestPlaylistId).Returns(fixtureVideos);

        var result = await _syncService.SyncPlaylistAsync(_testPlaylist);

        Assert.Equal(1, result.Added);
        Assert.Equal(0, result.Removed);
        Assert.Equal(0, result.Updated);
    }

    [Fact]
    public async Task Sync_TitleChanged_DetectedAsUpdate()
    {
        var fixtureVideos = FixtureLoader.LoadPlaylistVideos(TestPlaylistId);

        var dbVideos = fixtureVideos.Select((v, i) => new Video
        {
            Id = i + 1,
            PlaylistId = 1,
            YouTubeVideoId = v.VideoId,
            Title = i == 0 ? "Old Title That Changed" : v.Title,
            ChannelTitle = v.ChannelTitle,
            Position = v.Position,
        }).ToList();
        _playlistRepo.GetVideosAsync(1).Returns(dbVideos);
        _youtubeApi.GetPlaylistVideosAsync(TestPlaylistId).Returns(fixtureVideos);

        var result = await _syncService.SyncPlaylistAsync(_testPlaylist);

        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Removed);
        Assert.Equal(1, result.Updated);
        await _playlistRepo.Received(1).UpdateVideoAsync(
            Arg.Is<Video>(v => v.Title == fixtureVideos[0].Title));
    }
}
