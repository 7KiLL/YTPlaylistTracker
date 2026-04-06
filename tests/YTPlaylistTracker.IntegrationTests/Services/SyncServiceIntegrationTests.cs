using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using YTPlaylistTracker.Application.Services;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Enums;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Domain.Models;
using YTPlaylistTracker.Infrastructure.Data;

namespace YTPlaylistTracker.IntegrationTests.Services;

public class SyncServiceIntegrationTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PlaylistRepository _playlistRepo;
    private readonly IYouTubeApiService _youtubeApi = Substitute.For<IYouTubeApiService>();
    private readonly SyncService _syncService;
    private readonly Playlist _testPlaylist;

    public SyncServiceIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var repoLogger = Substitute.For<ILogger<PlaylistRepository>>();
        _playlistRepo = new PlaylistRepository(_db, repoLogger);

        var syncLogger = Substitute.For<ILogger<SyncService>>();
        _syncService = new SyncService(_youtubeApi, _playlistRepo, syncLogger);

        // Seed
        var profile = new Profile { Name = "Test", IsDefault = true };
        _db.Profiles.Add(profile);
        _db.SaveChanges();

        _testPlaylist = new Playlist { ProfileId = profile.Id, YouTubePlaylistId = "PLintegration", Title = "Integration Test", IsTracked = true };
        _db.Playlists.Add(_testPlaylist);
        _db.SaveChanges();
    }

    [Fact]
    public async Task FirstSync_PopulatesDb()
    {
        _youtubeApi.GetPlaylistVideosAsync("PLintegration").Returns([
            new YouTubeVideoSnapshot("v1", "First Video", "Channel A", 0),
            new YouTubeVideoSnapshot("v2", "Second Video", "Channel B", 1),
            new YouTubeVideoSnapshot("v3", "Third Video", "Channel C", 2),
        ]);

        var result = await _syncService.SyncPlaylistAsync(_testPlaylist);

        Assert.Equal(3, result.Added);
        Assert.Equal(0, result.Removed);

        var videos = await _playlistRepo.GetActiveVideosAsync(_testPlaylist.Id);
        Assert.Equal(3, videos.Count);
    }

    [Fact]
    public async Task SecondSync_WithRemovedVideo_SetsDeletedAt()
    {
        // First sync: 3 videos
        _youtubeApi.GetPlaylistVideosAsync("PLintegration").Returns([
            new YouTubeVideoSnapshot("v1", "First", "Ch", 0),
            new YouTubeVideoSnapshot("v2", "Second", "Ch", 1),
            new YouTubeVideoSnapshot("v3", "Third", "Ch", 2),
        ]);
        await _syncService.SyncPlaylistAsync(_testPlaylist);

        // Second sync: v2 is gone
        _youtubeApi.GetPlaylistVideosAsync("PLintegration").Returns([
            new YouTubeVideoSnapshot("v1", "First", "Ch", 0),
            new YouTubeVideoSnapshot("v3", "Third", "Ch", 1),
        ]);
        _youtubeApi.CheckVideoStatusAsync("v2").Returns(RemovalReason.Deleted);

        var result = await _syncService.SyncPlaylistAsync(_testPlaylist);

        Assert.Equal(0, result.Added);
        Assert.Equal(1, result.Removed);

        var deleted = await _playlistRepo.GetDeletedVideosAsync(_testPlaylist.Id);
        Assert.Single(deleted);
        Assert.Equal("Second", deleted[0].Title); // Title preserved!
        Assert.Equal(RemovalReason.Deleted, deleted[0].RemovalReason);
    }

    [Fact]
    public async Task ReAddedVideo_ClearsDeletedAt()
    {
        // First sync
        _youtubeApi.GetPlaylistVideosAsync("PLintegration").Returns([
            new YouTubeVideoSnapshot("v1", "Video", "Ch", 0),
        ]);
        await _syncService.SyncPlaylistAsync(_testPlaylist);

        // Second sync: removed
        _youtubeApi.GetPlaylistVideosAsync("PLintegration").Returns(new List<YouTubeVideoSnapshot>());
        _youtubeApi.CheckVideoStatusAsync("v1").Returns(RemovalReason.RemovedByOwner);
        await _syncService.SyncPlaylistAsync(_testPlaylist);

        var deleted = await _playlistRepo.GetDeletedVideosAsync(_testPlaylist.Id);
        Assert.Single(deleted);

        // Third sync: re-added
        _youtubeApi.GetPlaylistVideosAsync("PLintegration").Returns([
            new YouTubeVideoSnapshot("v1", "Video Restored", "Ch", 0),
        ]);
        var result = await _syncService.SyncPlaylistAsync(_testPlaylist);

        Assert.Equal(1, result.Added);
        var active = await _playlistRepo.GetActiveVideosAsync(_testPlaylist.Id);
        Assert.Single(active);
        Assert.Equal("Video Restored", active[0].Title);
        Assert.Null(active[0].DeletedAt);
        Assert.Null(active[0].RemovalReason);
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
