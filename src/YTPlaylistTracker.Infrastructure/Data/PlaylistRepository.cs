using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;

namespace YTPlaylistTracker.Infrastructure.Data;

public class PlaylistRepository(AppDbContext db, ILogger<PlaylistRepository> logger) : IPlaylistRepository
{
    public async Task<IReadOnlyList<Playlist>> GetByProfileAsync(int profileId, CancellationToken ct = default)
    {
        return await db.Playlists
            .Where(p => p.ProfileId == profileId)
            .OrderBy(p => p.Title)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Playlist>> GetTrackedByProfileAsync(int profileId, CancellationToken ct = default)
    {
        return await db.Playlists
            .Where(p => p.ProfileId == profileId && p.IsTracked)
            .ToListAsync(ct);
    }

    public async Task<Playlist?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Playlists.FindAsync([id], ct);
    }

    public async Task AddAsync(Playlist playlist, CancellationToken ct = default)
    {
        logger.LogInformation("Adding playlist: {PlaylistId} ({Title})", playlist.YouTubePlaylistId, playlist.Title);
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddPlaylistsAsync(IEnumerable<Playlist> playlists, CancellationToken ct = default)
    {
        db.Playlists.AddRange(playlists);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Playlist playlist, CancellationToken ct = default)
    {
        db.Playlists.Update(playlist);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var playlist = await db.Playlists.FindAsync([id], ct);
        if (playlist is not null)
        {
            logger.LogInformation("Deleting playlist: {PlaylistId}", playlist.YouTubePlaylistId);
            db.Playlists.Remove(playlist);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<Video>> GetVideosAsync(int playlistId, CancellationToken ct = default)
    {
        return await db.Videos
            .Where(v => v.PlaylistId == playlistId)
            .OrderBy(v => v.DeletedAt.HasValue)
            .ThenBy(v => v.Title)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Video>> GetActiveVideosAsync(int playlistId, CancellationToken ct = default)
    {
        return await db.Videos
            .Where(v => v.PlaylistId == playlistId && v.DeletedAt == null)
            .OrderBy(v => v.Title)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Video>> GetDeletedVideosAsync(int playlistId, CancellationToken ct = default)
    {
        return await db.Videos
            .Where(v => v.PlaylistId == playlistId && v.DeletedAt != null)
            .OrderByDescending(v => v.DeletedAt)
            .ToListAsync(ct);
    }

    public async Task AddVideoAsync(Video video, CancellationToken ct = default)
    {
        logger.LogDebug("Adding video: {VideoId} ({Title})", video.YouTubeVideoId, video.Title);
        db.Videos.Add(video);
        await db.SaveChangesAsync(ct);
    }

    public async Task AddVideosAsync(IEnumerable<Video> videos, CancellationToken ct = default)
    {
        db.Videos.AddRange(videos);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateVideoAsync(Video video, CancellationToken ct = default)
    {
        db.Videos.Update(video);
        await db.SaveChangesAsync(ct);
    }

    public async Task PurgeDeletedVideosAsync(int playlistId, CancellationToken ct = default)
    {
        var count = await db.Videos
            .Where(v => v.PlaylistId == playlistId && v.DeletedAt != null)
            .ExecuteDeleteAsync(ct);

        logger.LogInformation("Purging {Count} deleted videos from playlist {PlaylistId}", count, playlistId);
    }

    public async Task<IReadOnlyList<(Playlist Playlist, Video Video)>> GetAllDeletedVideosAsync(int profileId, CancellationToken ct = default)
    {
        var results = await db.Videos
            .Include(v => v.Playlist)
            .Where(v => v.Playlist.ProfileId == profileId && v.DeletedAt != null)
            .OrderByDescending(v => v.DeletedAt)
            .ToListAsync(ct);

        return results.Select(v => (v.Playlist, v)).ToList();
    }
}
