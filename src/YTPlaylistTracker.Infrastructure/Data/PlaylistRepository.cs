using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;

namespace YTPlaylistTracker.Infrastructure.Data;

public class PlaylistRepository(AppDbContext db, ILogger<PlaylistRepository> logger) : IPlaylistRepository
{
    public async Task<List<Playlist>> GetByProfileAsync(int profileId)
    {
        return await db.Playlists
            .Where(p => p.ProfileId == profileId)
            .OrderBy(p => p.Title)
            .ToListAsync();
    }

    public async Task<List<Playlist>> GetTrackedByProfileAsync(int profileId)
    {
        return await db.Playlists
            .Where(p => p.ProfileId == profileId && p.IsTracked)
            .ToListAsync();
    }

    public async Task<Playlist?> GetByIdAsync(int id)
    {
        return await db.Playlists.FindAsync(id);
    }

    public async Task AddAsync(Playlist playlist)
    {
        logger.LogInformation("Adding playlist: {PlaylistId} ({Title})", playlist.YouTubePlaylistId, playlist.Title);
        db.Playlists.Add(playlist);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Playlist playlist)
    {
        db.Playlists.Update(playlist);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var playlist = await db.Playlists.FindAsync(id);
        if (playlist is not null)
        {
            logger.LogInformation("Deleting playlist: {PlaylistId}", playlist.YouTubePlaylistId);
            db.Playlists.Remove(playlist);
            await db.SaveChangesAsync();
        }
    }

    public async Task<List<Video>> GetVideosAsync(int playlistId)
    {
        return await db.Videos
            .Where(v => v.PlaylistId == playlistId)
            .OrderBy(v => v.DeletedAt.HasValue)
            .ThenBy(v => v.Title)
            .ToListAsync();
    }

    public async Task<List<Video>> GetActiveVideosAsync(int playlistId)
    {
        return await db.Videos
            .Where(v => v.PlaylistId == playlistId && v.DeletedAt == null)
            .OrderBy(v => v.Title)
            .ToListAsync();
    }

    public async Task<List<Video>> GetDeletedVideosAsync(int playlistId)
    {
        return await db.Videos
            .Where(v => v.PlaylistId == playlistId && v.DeletedAt != null)
            .OrderByDescending(v => v.DeletedAt)
            .ToListAsync();
    }

    public async Task AddVideoAsync(Video video)
    {
        logger.LogDebug("Adding video: {VideoId} ({Title})", video.YouTubeVideoId, video.Title);
        db.Videos.Add(video);
        await db.SaveChangesAsync();
    }

    public async Task AddVideosAsync(IEnumerable<Video> videos)
    {
        db.Videos.AddRange(videos);
        await db.SaveChangesAsync();
    }

    public async Task UpdateVideoAsync(Video video)
    {
        db.Videos.Update(video);
        await db.SaveChangesAsync();
    }

    public async Task PurgeDeletedVideosAsync(int playlistId)
    {
        var deleted = await db.Videos
            .Where(v => v.PlaylistId == playlistId && v.DeletedAt != null)
            .ToListAsync();

        logger.LogInformation("Purging {Count} deleted videos from playlist {PlaylistId}", deleted.Count, playlistId);
        db.Videos.RemoveRange(deleted);
        await db.SaveChangesAsync();
    }
}
