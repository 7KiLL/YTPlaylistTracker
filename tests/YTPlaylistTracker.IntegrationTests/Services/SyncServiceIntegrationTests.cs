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

public class SyncServiceIntegrationTests
{
    private AppDbContext _db = null!;
    private PlaylistRepository _playlistRepo = null!;
    private readonly IYouTubeApiService _youtubeApi = Substitute.For<IYouTubeApiService>();
    private SyncService _syncService = null!;
    private Playlist _testPlaylist = null!;

    [Before(Test)]
    public void Setup()
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
        _syncService = new SyncService(_playlistRepo, syncLogger);

        // Seed
        var profile = new Profile { Name = "Test", IsDefault = true };
        _db.Profiles.Add(profile);
        _db.SaveChanges();

        _testPlaylist = new Playlist { ProfileId = profile.Id, Profile = profile, YouTubePlaylistId = "PLintegration", Title = "Integration Test", IsTracked = true };
        _db.Playlists.Add(_testPlaylist);
        _db.SaveChanges();
    }

    [Test]
    public async Task FirstSync_PopulatesDb()
    {
        _youtubeApi.GetPlaylistVideosAsync("PLintegration").Returns([
            new YouTubeVideoSnapshot("v1", "First Video", "Channel A", 0),
            new YouTubeVideoSnapshot("v2", "Second Video", "Channel B", 1),
            new YouTubeVideoSnapshot("v3", "Third Video", "Channel C", 2),
        ]);

        var result = await _syncService.SyncPlaylistAsync(_testPlaylist, _youtubeApi);

        await Assert.That(result.Added).IsEqualTo(3);
        await Assert.That(result.Removed).IsEqualTo(0);

        var all = await _playlistRepo.GetVideosAsync(_testPlaylist.Id);
        var videos = all.Where(v => v.DeletedAt == null).ToList();
        await Assert.That(videos.Count).IsEqualTo(3);
    }

    [Test]
    public async Task SecondSync_WithRemovedVideo_SetsDeletedAt()
    {
        // First sync: 3 videos
        _youtubeApi.GetPlaylistVideosAsync("PLintegration").Returns([
            new YouTubeVideoSnapshot("v1", "First", "Ch", 0),
            new YouTubeVideoSnapshot("v2", "Second", "Ch", 1),
            new YouTubeVideoSnapshot("v3", "Third", "Ch", 2),
        ]);
        await _syncService.SyncPlaylistAsync(_testPlaylist, _youtubeApi);

        // Second sync: v2 is gone
        _youtubeApi.GetPlaylistVideosAsync("PLintegration").Returns([
            new YouTubeVideoSnapshot("v1", "First", "Ch", 0),
            new YouTubeVideoSnapshot("v3", "Third", "Ch", 1),
        ]);
        _youtubeApi.CheckVideoStatusAsync("v2").Returns(RemovalReason.Deleted);

        var result = await _syncService.SyncPlaylistAsync(_testPlaylist, _youtubeApi);

        await Assert.That(result.Added).IsEqualTo(0);
        await Assert.That(result.Removed).IsEqualTo(1);

        var deleted = await _playlistRepo.GetDeletedVideosAsync(_testPlaylist.Id);
        await Assert.That(deleted).HasCount().EqualTo(1);
        await Assert.That(deleted[0].Title).IsEqualTo("Second"); // Title preserved!
        await Assert.That(deleted[0].RemovalReason).IsEqualTo(RemovalReason.Deleted);
    }

    [Test]
    public async Task ReAddedVideo_ClearsDeletedAt()
    {
        // First sync
        _youtubeApi.GetPlaylistVideosAsync("PLintegration").Returns([
            new YouTubeVideoSnapshot("v1", "Video", "Ch", 0),
        ]);
        await _syncService.SyncPlaylistAsync(_testPlaylist, _youtubeApi);

        // Second sync: removed
        _youtubeApi.GetPlaylistVideosAsync("PLintegration").Returns(new List<YouTubeVideoSnapshot>());
        _youtubeApi.CheckVideoStatusAsync("v1").Returns(RemovalReason.RemovedByOwner);
        await _syncService.SyncPlaylistAsync(_testPlaylist, _youtubeApi);

        var deleted = await _playlistRepo.GetDeletedVideosAsync(_testPlaylist.Id);
        await Assert.That(deleted).HasCount().EqualTo(1);

        // Third sync: re-added
        _youtubeApi.GetPlaylistVideosAsync("PLintegration").Returns([
            new YouTubeVideoSnapshot("v1", "Video Restored", "Ch", 0),
        ]);
        var result = await _syncService.SyncPlaylistAsync(_testPlaylist, _youtubeApi);

        await Assert.That(result.Added).IsEqualTo(1);
        var allVideos = await _playlistRepo.GetVideosAsync(_testPlaylist.Id);
        var active = allVideos.Where(v => v.DeletedAt == null).ToList();
        await Assert.That(active).HasCount().EqualTo(1);
        await Assert.That(active[0].Title).IsEqualTo("Video Restored");
        await Assert.That(active[0].DeletedAt).IsNull();
        await Assert.That(active[0].RemovalReason).IsNull();
    }

    [After(Test)]
    public void Cleanup()
    {
        _db.Dispose();
    }
}
