using NSubstitute;
using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Application.Services;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Enums;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Domain.Models;

namespace YTPlaylistTracker.UnitTests.Services;

public class SyncServiceTests
{
    private readonly IYouTubeApiService _youtubeApi = Substitute.For<IYouTubeApiService>();
    private readonly IPlaylistRepository _playlistRepo = Substitute.For<IPlaylistRepository>();
    private readonly ISyncService _syncService;

    private readonly Playlist _testPlaylist = new()
    {
        Id = 1,
        ProfileId = 1,
        YouTubePlaylistId = "PLtest123",
        Title = "Test Playlist",
        IsTracked = true
    };

    public SyncServiceTests()
    {
        var logger = Substitute.For<ILogger<SyncService>>();
        _syncService = new SyncService(_youtubeApi, _playlistRepo, logger);
    }

    [Fact]
    public async Task SyncPlaylist_NewVideos_DetectedAsAdditions()
    {
        _youtubeApi.GetPlaylistVideosAsync("PLtest123").Returns([
            new YouTubeVideoSnapshot("vid1", "Video One", "Channel A", 0),
            new YouTubeVideoSnapshot("vid2", "Video Two", "Channel B", 1),
        ]);
        _playlistRepo.GetVideosAsync(1).Returns(new List<Video>());

        var result = await _syncService.SyncPlaylistAsync(_testPlaylist);

        Assert.Equal(2, result.Added);
        Assert.Equal(0, result.Removed);
        Assert.Equal(0, result.Updated);
        await _playlistRepo.Received(2).AddVideoAsync(Arg.Any<Video>());
    }

    [Fact]
    public async Task SyncPlaylist_MissingVideos_MarkedAsDeleted()
    {
        _youtubeApi.GetPlaylistVideosAsync("PLtest123").Returns(new List<YouTubeVideoSnapshot>());
        _youtubeApi.CheckVideoStatusAsync("vid1").Returns(RemovalReason.Deleted);

        _playlistRepo.GetVideosAsync(1).Returns([
            new Video { Id = 10, PlaylistId = 1, YouTubeVideoId = "vid1", Title = "Gone Video", ChannelTitle = "Ch" }
        ]);

        var result = await _syncService.SyncPlaylistAsync(_testPlaylist);

        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.Removed);
        await _playlistRepo.Received(1).UpdateVideoAsync(Arg.Is<Video>(v =>
            v.YouTubeVideoId == "vid1" && v.DeletedAt != null && v.RemovalReason == RemovalReason.Deleted));
    }

    [Fact]
    public async Task SyncPlaylist_TitlePreservedOnDeletion()
    {
        _youtubeApi.GetPlaylistVideosAsync("PLtest123").Returns(new List<YouTubeVideoSnapshot>());
        _youtubeApi.CheckVideoStatusAsync("vid1").Returns(RemovalReason.Private);

        var video = new Video { Id = 10, PlaylistId = 1, YouTubeVideoId = "vid1", Title = "Important Title", ChannelTitle = "Author" };
        _playlistRepo.GetVideosAsync(1).Returns([video]);

        await _syncService.SyncPlaylistAsync(_testPlaylist);

        // Title must still be "Important Title" after marking as deleted
        await _playlistRepo.Received().UpdateVideoAsync(Arg.Is<Video>(v =>
            v.Title == "Important Title" && v.DeletedAt != null));
    }

    [Fact]
    public async Task SyncPlaylist_ReAddedVideo_ClearsDeletedAt()
    {
        _youtubeApi.GetPlaylistVideosAsync("PLtest123").Returns([
            new YouTubeVideoSnapshot("vid1", "Restored Video", "Channel", 0),
        ]);

        _playlistRepo.GetVideosAsync(1).Returns([
            new Video { Id = 10, PlaylistId = 1, YouTubeVideoId = "vid1", Title = "Old Title",
                        DeletedAt = DateTime.UtcNow.AddDays(-1), RemovalReason = RemovalReason.Deleted }
        ]);

        var result = await _syncService.SyncPlaylistAsync(_testPlaylist);

        Assert.Equal(1, result.Added); // Re-addition counts as added
        await _playlistRepo.Received().UpdateVideoAsync(Arg.Is<Video>(v =>
            v.YouTubeVideoId == "vid1" && v.DeletedAt == null && v.RemovalReason == null));
    }

    [Fact]
    public async Task SyncPlaylist_ExistingVideo_UpdatesTitleAndChannel()
    {
        _youtubeApi.GetPlaylistVideosAsync("PLtest123").Returns([
            new YouTubeVideoSnapshot("vid1", "New Title", "New Channel", 0),
        ]);

        _playlistRepo.GetVideosAsync(1).Returns([
            new Video { Id = 10, PlaylistId = 1, YouTubeVideoId = "vid1", Title = "Old Title", ChannelTitle = "Old Channel" }
        ]);

        var result = await _syncService.SyncPlaylistAsync(_testPlaylist);

        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Removed);
        Assert.Equal(1, result.Updated);
        await _playlistRepo.Received().UpdateVideoAsync(Arg.Is<Video>(v =>
            v.Title == "New Title" && v.ChannelTitle == "New Channel"));
    }

    [Fact]
    public async Task SyncPlaylist_EmptyPlaylist_NoErrors()
    {
        _youtubeApi.GetPlaylistVideosAsync("PLtest123").Returns(new List<YouTubeVideoSnapshot>());
        _playlistRepo.GetVideosAsync(1).Returns(new List<Video>());

        var result = await _syncService.SyncPlaylistAsync(_testPlaylist);

        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Removed);
        Assert.Equal(0, result.Updated);
    }

    [Fact]
    public async Task SyncPlaylist_RemovalReasonCorrectlyAssigned()
    {
        _youtubeApi.GetPlaylistVideosAsync("PLtest123").Returns(new List<YouTubeVideoSnapshot>());
        _youtubeApi.CheckVideoStatusAsync("vid1").Returns(RemovalReason.Private);
        _youtubeApi.CheckVideoStatusAsync("vid2").Returns(RemovalReason.Deleted);

        _playlistRepo.GetVideosAsync(1).Returns([
            new Video { Id = 10, PlaylistId = 1, YouTubeVideoId = "vid1", Title = "Private Vid" },
            new Video { Id = 11, PlaylistId = 1, YouTubeVideoId = "vid2", Title = "Deleted Vid" },
        ]);

        await _syncService.SyncPlaylistAsync(_testPlaylist);

        await _playlistRepo.Received().UpdateVideoAsync(Arg.Is<Video>(v =>
            v.YouTubeVideoId == "vid1" && v.RemovalReason == RemovalReason.Private));
        await _playlistRepo.Received().UpdateVideoAsync(Arg.Is<Video>(v =>
            v.YouTubeVideoId == "vid2" && v.RemovalReason == RemovalReason.Deleted));
    }

    [Fact]
    public async Task SyncPlaylist_UnchangedVideo_NotUpdated()
    {
        _youtubeApi.GetPlaylistVideosAsync("PLtest123").Returns([
            new YouTubeVideoSnapshot("vid1", "Same Title", "Same Channel", 0),
        ]);

        _playlistRepo.GetVideosAsync(1).Returns([
            new Video { Id = 10, PlaylistId = 1, YouTubeVideoId = "vid1", Title = "Same Title", ChannelTitle = "Same Channel" }
        ]);

        var result = await _syncService.SyncPlaylistAsync(_testPlaylist);

        Assert.Equal(0, result.Updated);
    }
}
