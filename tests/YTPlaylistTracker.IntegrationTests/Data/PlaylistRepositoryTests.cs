using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Enums;
using YTPlaylistTracker.Infrastructure.Data;

namespace YTPlaylistTracker.IntegrationTests.Data;

public class PlaylistRepositoryTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PlaylistRepository _repo;

    public PlaylistRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var logger = Substitute.For<ILogger<PlaylistRepository>>();
        _repo = new PlaylistRepository(_db, logger);

        // Seed a profile
        _db.Profiles.Add(new Profile { Id = 1, Name = "Test", IsDefault = true });
        _db.SaveChanges();
    }

    [Fact]
    public async Task AddPlaylist_CanQueryBack()
    {
        var playlist = new Playlist { ProfileId = 1, YouTubePlaylistId = "PL123", Title = "Test Playlist", IsTracked = true };
        await _repo.AddAsync(playlist);

        var result = await _repo.GetByProfileAsync(1);
        Assert.Single(result);
        Assert.Equal("PL123", result[0].YouTubePlaylistId);
        Assert.Equal("Test Playlist", result[0].Title);
    }

    [Fact]
    public async Task AddVideos_QueryByPlaylist()
    {
        var playlist = new Playlist { ProfileId = 1, YouTubePlaylistId = "PL456", IsTracked = true };
        await _repo.AddAsync(playlist);

        await _repo.AddVideoAsync(new Video { PlaylistId = playlist.Id, YouTubeVideoId = "v1", Title = "Video 1", ChannelTitle = "Ch1" });
        await _repo.AddVideoAsync(new Video { PlaylistId = playlist.Id, YouTubeVideoId = "v2", Title = "Video 2" });

        var videos = await _repo.GetVideosAsync(playlist.Id);
        Assert.Equal(2, videos.Count);
    }

    [Fact]
    public async Task SoftDeleteVideo_AppearsInDeleted()
    {
        var playlist = new Playlist { ProfileId = 1, YouTubePlaylistId = "PL789", IsTracked = true };
        await _repo.AddAsync(playlist);

        var video = new Video { PlaylistId = playlist.Id, YouTubeVideoId = "v1", Title = "Will Be Removed" };
        await _repo.AddVideoAsync(video);

        video.DeletedAt = DateTime.UtcNow;
        video.RemovalReason = RemovalReason.Deleted;
        await _repo.UpdateVideoAsync(video);

        var active = await _repo.GetActiveVideosAsync(playlist.Id);
        var deleted = await _repo.GetDeletedVideosAsync(playlist.Id);

        Assert.Empty(active);
        Assert.Single(deleted);
        Assert.Equal("Will Be Removed", deleted[0].Title);
    }

    [Fact]
    public async Task UniqueConstraint_DuplicateVideoId_Throws()
    {
        var playlist = new Playlist { ProfileId = 1, YouTubePlaylistId = "PLunique", IsTracked = true };
        await _repo.AddAsync(playlist);

        await _repo.AddVideoAsync(new Video { PlaylistId = playlist.Id, YouTubeVideoId = "dup1", Title = "First" });

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            _repo.AddVideoAsync(new Video { PlaylistId = playlist.Id, YouTubeVideoId = "dup1", Title = "Duplicate" }));
    }

    [Fact]
    public async Task PurgeDeletedVideos_RemovesPermanently()
    {
        var playlist = new Playlist { ProfileId = 1, YouTubePlaylistId = "PLpurge", IsTracked = true };
        await _repo.AddAsync(playlist);

        await _repo.AddVideoAsync(new Video { PlaylistId = playlist.Id, YouTubeVideoId = "v1", Title = "Active" });
        var deletedVideo = new Video { PlaylistId = playlist.Id, YouTubeVideoId = "v2", Title = "Deleted", DeletedAt = DateTime.UtcNow };
        await _repo.AddVideoAsync(deletedVideo);

        await _repo.PurgeDeletedVideosAsync(playlist.Id);

        var all = await _repo.GetVideosAsync(playlist.Id);
        Assert.Single(all);
        Assert.Equal("Active", all[0].Title);
    }

    [Fact]
    public async Task GetTrackedByProfile_FiltersCorrectly()
    {
        await _repo.AddAsync(new Playlist { ProfileId = 1, YouTubePlaylistId = "tracked1", IsTracked = true });
        await _repo.AddAsync(new Playlist { ProfileId = 1, YouTubePlaylistId = "untracked1", IsTracked = false });
        await _repo.AddAsync(new Playlist { ProfileId = 1, YouTubePlaylistId = "tracked2", IsTracked = true });

        var tracked = await _repo.GetTrackedByProfileAsync(1);
        Assert.Equal(2, tracked.Count);
        Assert.All(tracked, p => Assert.True(p.IsTracked));
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
