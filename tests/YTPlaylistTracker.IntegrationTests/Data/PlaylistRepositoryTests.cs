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
    private readonly Profile _profile;

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
        _profile = new Profile { Id = 1, Name = "Test", IsDefault = true };
        _db.Profiles.Add(_profile);
        _db.SaveChanges();
    }

    [Fact]
    public async Task AddPlaylist_CanQueryBack()
    {
        var playlist = new Playlist { ProfileId = 1, Profile = _profile, YouTubePlaylistId = "PL123", Title = "Test Playlist", IsTracked = true };
        await _repo.AddAsync(playlist);

        var result = await _repo.GetByProfileAsync(1);
        Assert.Single(result);
        Assert.Equal("PL123", result[0].YouTubePlaylistId);
        Assert.Equal("Test Playlist", result[0].Title);
    }

    [Fact]
    public async Task AddVideos_QueryByPlaylist()
    {
        var playlist = new Playlist { ProfileId = 1, Profile = _profile, YouTubePlaylistId = "PL456", IsTracked = true };
        await _repo.AddAsync(playlist);

        await _repo.AddVideosAsync([new Video { PlaylistId = playlist.Id, Playlist = playlist, YouTubeVideoId = "v1", Title = "Video 1", ChannelTitle = "Ch1" }]);
        await _repo.AddVideosAsync([new Video { PlaylistId = playlist.Id, Playlist = playlist, YouTubeVideoId = "v2", Title = "Video 2" }]);

        var videos = await _repo.GetVideosAsync(playlist.Id);
        Assert.Equal(2, videos.Count);
    }

    [Fact]
    public async Task SoftDeleteVideo_AppearsInDeleted()
    {
        var playlist = new Playlist { ProfileId = 1, Profile = _profile, YouTubePlaylistId = "PL789", IsTracked = true };
        await _repo.AddAsync(playlist);

        var video = new Video { PlaylistId = playlist.Id, Playlist = playlist, YouTubeVideoId = "v1", Title = "Will Be Removed" };
        await _repo.AddVideosAsync([video]);

        video.DeletedAt = DateTime.UtcNow;
        video.RemovalReason = RemovalReason.Deleted;
        await _repo.UpdateVideoAsync(video);

        var all = await _repo.GetVideosAsync(playlist.Id);
        var active = all.Where(v => v.DeletedAt == null).ToList();
        var deleted = await _repo.GetDeletedVideosAsync(playlist.Id);

        Assert.Empty(active);
        Assert.Single(deleted);
        Assert.Equal("Will Be Removed", deleted[0].Title);
    }

    [Fact]
    public async Task UniqueConstraint_DuplicateVideoId_Throws()
    {
        var playlist = new Playlist { ProfileId = 1, Profile = _profile, YouTubePlaylistId = "PLunique", IsTracked = true };
        await _repo.AddAsync(playlist);

        await _repo.AddVideosAsync([new Video { PlaylistId = playlist.Id, Playlist = playlist, YouTubeVideoId = "dup1", Title = "First" }]);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            _repo.AddVideosAsync([new Video { PlaylistId = playlist.Id, Playlist = playlist, YouTubeVideoId = "dup1", Title = "Duplicate" }]));
    }

    [Fact]
    public async Task PurgeDeletedVideos_RemovesPermanently()
    {
        var playlist = new Playlist { ProfileId = 1, Profile = _profile, YouTubePlaylistId = "PLpurge", IsTracked = true };
        await _repo.AddAsync(playlist);

        await _repo.AddVideosAsync([new Video { PlaylistId = playlist.Id, Playlist = playlist, YouTubeVideoId = "v1", Title = "Active" }]);
        var deletedVideo = new Video { PlaylistId = playlist.Id, Playlist = playlist, YouTubeVideoId = "v2", Title = "Deleted", DeletedAt = DateTime.UtcNow };
        await _repo.AddVideosAsync([deletedVideo]);

        await _repo.PurgeDeletedVideosAsync(playlist.Id);

        var all = await _repo.GetVideosAsync(playlist.Id);
        Assert.Single(all);
        Assert.Equal("Active", all[0].Title);
    }

    [Fact]
    public async Task GetTrackedByProfile_FiltersCorrectly()
    {
        await _repo.AddAsync(new Playlist { ProfileId = 1, Profile = _profile, YouTubePlaylistId = "tracked1", IsTracked = true });
        await _repo.AddAsync(new Playlist { ProfileId = 1, Profile = _profile, YouTubePlaylistId = "untracked1", IsTracked = false });
        await _repo.AddAsync(new Playlist { ProfileId = 1, Profile = _profile, YouTubePlaylistId = "tracked2", IsTracked = true });

        var tracked = await _repo.GetTrackedByProfileAsync(1);
        Assert.Equal(2, tracked.Count);
        Assert.All(tracked, p => Assert.True(p.IsTracked));
    }

    [Fact]
    public async Task GetAllDeletedVideosAsync_ReturnsAcrossPlaylists()
    {
        var playlist1 = new Playlist { ProfileId = 1, Profile = _profile, YouTubePlaylistId = "PLcross1", Title = "Playlist A", IsTracked = true };
        var playlist2 = new Playlist { ProfileId = 1, Profile = _profile, YouTubePlaylistId = "PLcross2", Title = "Playlist B", IsTracked = true };
        await _repo.AddAsync(playlist1);
        await _repo.AddAsync(playlist2);

        // Active video — should not appear
        await _repo.AddVideosAsync([new Video { PlaylistId = playlist1.Id, Playlist = playlist1, YouTubeVideoId = "active1", Title = "Active" }]);

        // Deleted videos across both playlists
        await _repo.AddVideosAsync([new Video
        {
            PlaylistId = playlist1.Id, Playlist = playlist1, YouTubeVideoId = "del1", Title = "Deleted From A",
            DeletedAt = new DateTime(2025, 6, 1), RemovalReason = RemovalReason.Deleted
        }]);
        await _repo.AddVideosAsync([new Video
        {
            PlaylistId = playlist2.Id, Playlist = playlist2, YouTubeVideoId = "del2", Title = "Private In B",
            DeletedAt = new DateTime(2025, 7, 1), RemovalReason = RemovalReason.Private
        }]);

        var results = await _repo.GetAllDeletedVideosAsync(1);

        Assert.Equal(2, results.Count);
        // Should be ordered by DeletedAt descending (most recent first)
        Assert.Equal("Private In B", results[0].Video.Title);
        Assert.Equal("Playlist B", results[0].Playlist.Title);
        Assert.Equal("Deleted From A", results[1].Video.Title);
        Assert.Equal("Playlist A", results[1].Playlist.Title);
    }

    [Fact]
    public async Task GetAllDeletedVideosAsync_FiltersOtherProfiles()
    {
        // Add a second profile
        var otherProfile = new Profile { Id = 2, Name = "Other" };
        _db.Profiles.Add(otherProfile);
        await _db.SaveChangesAsync();

        var playlist1 = new Playlist { ProfileId = 1, Profile = _profile, YouTubePlaylistId = "PLprof1", IsTracked = true };
        var playlist2 = new Playlist { ProfileId = 2, Profile = otherProfile, YouTubePlaylistId = "PLprof2", IsTracked = true };
        await _repo.AddAsync(playlist1);
        await _repo.AddAsync(playlist2);

        await _repo.AddVideosAsync([new Video
        {
            PlaylistId = playlist1.Id, Playlist = playlist1, YouTubeVideoId = "v1", Title = "Mine",
            DeletedAt = DateTime.UtcNow, RemovalReason = RemovalReason.Deleted
        }]);
        await _repo.AddVideosAsync([new Video
        {
            PlaylistId = playlist2.Id, Playlist = playlist2, YouTubeVideoId = "v2", Title = "Not Mine",
            DeletedAt = DateTime.UtcNow, RemovalReason = RemovalReason.Deleted
        }]);

        var profile1Results = await _repo.GetAllDeletedVideosAsync(1);
        var profile2Results = await _repo.GetAllDeletedVideosAsync(2);

        Assert.Single(profile1Results);
        Assert.Equal("Mine", profile1Results[0].Video.Title);
        Assert.Single(profile2Results);
        Assert.Equal("Not Mine", profile2Results[0].Video.Title);
    }

    [Fact]
    public async Task GetAllDeletedVideosAsync_EmptyWhenNoDeleted()
    {
        var playlist = new Playlist { ProfileId = 1, Profile = _profile, YouTubePlaylistId = "PLempty", IsTracked = true };
        await _repo.AddAsync(playlist);
        await _repo.AddVideosAsync([new Video { PlaylistId = playlist.Id, Playlist = playlist, YouTubeVideoId = "v1", Title = "Active" }]);

        var results = await _repo.GetAllDeletedVideosAsync(1);

        Assert.Empty(results);
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
