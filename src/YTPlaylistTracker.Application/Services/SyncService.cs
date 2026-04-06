using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;

namespace YTPlaylistTracker.Application.Services;

public class SyncService(
    IYouTubeApiService youtube,
    IPlaylistRepository playlistRepo,
    ILogger<SyncService> logger) : ISyncService
{
    public async Task<SyncResult> SyncPlaylistAsync(Playlist playlist)
    {
        logger.LogInformation("Syncing playlist: {PlaylistId} ({Title})", playlist.YouTubePlaylistId, playlist.Title);

        var apiVideos = await youtube.GetPlaylistVideosAsync(playlist.YouTubePlaylistId);
        // YouTube allows duplicate videos in a playlist — deduplicate, keeping first occurrence
        var apiVideoIds = apiVideos.GroupBy(v => v.VideoId).ToDictionary(g => g.Key, g => g.First());

        logger.LogDebug("API returned {Count} videos for playlist {PlaylistId}", apiVideos.Count, playlist.YouTubePlaylistId);

        var dbVideos = await playlistRepo.GetVideosAsync(playlist.Id);
        var activeDbVideos = dbVideos.Where(v => v.DeletedAt == null).ToList();
        var deletedDbVideos = dbVideos.Where(v => v.DeletedAt != null).ToDictionary(v => v.YouTubeVideoId);
        var activeDbVideoIds = activeDbVideos.ToDictionary(v => v.YouTubeVideoId);

        int added = 0, removed = 0, updated = 0;

        // Detect removals: in DB (active) but not in API
        foreach (var dbVideo in activeDbVideos)
        {
            if (!apiVideoIds.ContainsKey(dbVideo.YouTubeVideoId))
            {
                logger.LogWarning("Video removed: {VideoId} ({Title})", dbVideo.YouTubeVideoId, dbVideo.Title);
                dbVideo.DeletedAt = DateTime.UtcNow;
                dbVideo.RemovalReason = await youtube.CheckVideoStatusAsync(dbVideo.YouTubeVideoId);
                await playlistRepo.UpdateVideoAsync(dbVideo);
                removed++;
            }
        }

        // Detect additions and re-additions (use deduped dictionary values)
        foreach (var apiVideo in apiVideoIds.Values)
        {
            if (activeDbVideoIds.ContainsKey(apiVideo.VideoId))
            {
                // Update existing active video if title/channel/metadata changed
                var dbVideo = activeDbVideoIds[apiVideo.VideoId];
                bool changed = false;

                if (dbVideo.Title != apiVideo.Title)
                {
                    logger.LogDebug("Video title changed: {Old} → {New}", dbVideo.Title, apiVideo.Title);
                    dbVideo.Title = apiVideo.Title;
                    changed = true;
                }
                if (dbVideo.ChannelTitle != apiVideo.ChannelTitle)
                {
                    dbVideo.ChannelTitle = apiVideo.ChannelTitle;
                    changed = true;
                }
                if (dbVideo.Description != apiVideo.Description)
                {
                    dbVideo.Description = apiVideo.Description;
                    changed = true;
                }
                if (dbVideo.ThumbnailUrl != apiVideo.ThumbnailUrl)
                {
                    dbVideo.ThumbnailUrl = apiVideo.ThumbnailUrl;
                    changed = true;
                }
                if (dbVideo.Position != apiVideo.Position)
                {
                    dbVideo.Position = apiVideo.Position;
                    changed = true;
                }
                if (dbVideo.JsonMetadata != apiVideo.JsonMetadata)
                {
                    dbVideo.JsonMetadata = apiVideo.JsonMetadata;
                    changed = true;
                }

                if (changed)
                {
                    await playlistRepo.UpdateVideoAsync(dbVideo);
                    updated++;
                }
            }
            else if (deletedDbVideos.TryGetValue(apiVideo.VideoId, out var deletedVideo))
            {
                // Re-addition: was deleted, now back
                logger.LogInformation("Video re-added: {VideoId} ({Title})", apiVideo.VideoId, apiVideo.Title);
                deletedVideo.DeletedAt = null;
                deletedVideo.RemovalReason = null;
                deletedVideo.Title = apiVideo.Title;
                deletedVideo.ChannelTitle = apiVideo.ChannelTitle;
                deletedVideo.Description = apiVideo.Description;
                deletedVideo.ThumbnailUrl = apiVideo.ThumbnailUrl;
                deletedVideo.Position = apiVideo.Position;
                deletedVideo.JsonMetadata = apiVideo.JsonMetadata;
                deletedVideo.AddedAt = apiVideo.AddedAt;
                await playlistRepo.UpdateVideoAsync(deletedVideo);
                added++;
            }
            else
            {
                // New video
                logger.LogDebug("New video: {VideoId} ({Title})", apiVideo.VideoId, apiVideo.Title);
                await playlistRepo.AddVideoAsync(new Video
                {
                    PlaylistId = playlist.Id,
                    YouTubeVideoId = apiVideo.VideoId,
                    Title = apiVideo.Title,
                    ChannelTitle = apiVideo.ChannelTitle,
                    AddedAt = apiVideo.AddedAt,
                    Description = apiVideo.Description,
                    ThumbnailUrl = apiVideo.ThumbnailUrl,
                    Position = apiVideo.Position,
                    JsonMetadata = apiVideo.JsonMetadata,
                });
                added++;
            }
        }

        // Update playlist metadata
        var meta = await youtube.GetPlaylistMetadataAsync(playlist.YouTubePlaylistId);
        if (meta is not null)
        {
            if (playlist.Title is null || playlist.Title != meta.Title)
                playlist.Title = meta.Title;
            playlist.Description = meta.Description;
            playlist.ThumbnailUrl = meta.ThumbnailUrl;
            playlist.PublishedAt = meta.PublishedAt;
            if (meta.JsonMetadata is not null)
                playlist.JsonMetadata = meta.JsonMetadata;
        }

        playlist.LastSyncedAt = DateTime.UtcNow;
        await playlistRepo.UpdateAsync(playlist);

        var result = new SyncResult(added, removed, updated);
        logger.LogInformation("Sync complete for {PlaylistId}: +{Added} -{Removed} ~{Updated}",
            playlist.YouTubePlaylistId, result.Added, result.Removed, result.Updated);

        return result;
    }

    public async Task<Dictionary<int, SyncResult>> SyncAllTrackedAsync(int profileId)
    {
        var playlists = await playlistRepo.GetTrackedByProfileAsync(profileId);
        logger.LogInformation("Syncing {Count} tracked playlists for profile {ProfileId}", playlists.Count, profileId);

        var results = new Dictionary<int, SyncResult>();
        foreach (var playlist in playlists)
        {
            results[playlist.Id] = await SyncPlaylistAsync(playlist);
        }
        return results;
    }
}
