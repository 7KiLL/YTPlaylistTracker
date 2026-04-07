using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Domain.Models;

namespace YTPlaylistTracker.Application.Services;

public class SyncService(
    IYouTubeApiService youtube,
    IPlaylistRepository playlistRepo,
    ILogger<SyncService> logger) : ISyncService
{
    // --- Tunable cooldowns ---
    public static readonly TimeSpan LikedManualCooldown = TimeSpan.FromDays(1);
    public static readonly TimeSpan AutoSyncCooldown = TimeSpan.FromHours(6);
    private const int MaxConcurrentFetches = 4;

    // --- Helpers ---

    public static bool IsLikedPlaylist(string youtubePlaylistId)
        => youtubePlaylistId.StartsWith("LL", StringComparison.Ordinal);

    public static TimeSpan? GetRemainingCooldown(Playlist playlist)
    {
        if (!IsLikedPlaylist(playlist.YouTubePlaylistId) || !playlist.LastSyncedAt.HasValue)
            return null;
        var remaining = LikedManualCooldown - (DateTime.UtcNow - playlist.LastSyncedAt.Value);
        return remaining > TimeSpan.Zero ? remaining : null;
    }

    public static string FormatLastSynced(Playlist playlist)
    {
        if (!playlist.LastSyncedAt.HasValue)
            return "never";
        var ago = DateTime.UtcNow - playlist.LastSyncedAt.Value;
        return ago.TotalMinutes < 1 ? "just now"
            : ago.TotalMinutes < 60 ? $"{(int)ago.TotalMinutes}m ago"
            : ago.TotalHours < 24 ? $"{(int)ago.TotalHours}h ago"
            : $"{(int)ago.TotalDays}d ago";
    }

    private static bool IsAutoSyncCooldownActive(Playlist playlist)
    {
        if (!playlist.LastSyncedAt.HasValue) return false;
        // Liked playlists are never auto-synced
        if (IsLikedPlaylist(playlist.YouTubePlaylistId)) return true;
        return DateTime.UtcNow - playlist.LastSyncedAt.Value < AutoSyncCooldown;
    }

    // --- Sync operations ---

    public async Task<SyncResult> SyncPlaylistAsync(Playlist playlist)
    {
        logger.LogInformation("Syncing playlist: {PlaylistId} ({Title})", playlist.YouTubePlaylistId, playlist.Title);

        var apiVideos = await youtube.GetPlaylistVideosAsync(playlist.YouTubePlaylistId);
        var metadata = await youtube.GetPlaylistMetadataAsync(playlist.YouTubePlaylistId);

        return await ApplyPlaylistDiff(playlist, apiVideos, metadata);
    }

    public async Task<Dictionary<int, SyncResult>> SyncAllTrackedAsync(int profileId, IProgress<string>? progress = null)
    {
        var allPlaylists = await playlistRepo.GetTrackedByProfileAsync(profileId);

        // Filter out playlists on cooldown (Liked never auto-syncs, others skip if <6h)
        var playlists = allPlaylists.Where(p => !IsAutoSyncCooldownActive(p)).ToList();
        var skipped = allPlaylists.Count - playlists.Count;

        if (skipped > 0)
            logger.LogInformation("Auto-sync: skipping {Skipped} playlists on cooldown", skipped);

        logger.LogInformation("Syncing {Count} tracked playlists for profile {ProfileId}", playlists.Count, profileId);

        if (playlists.Count == 0)
            return new Dictionary<int, SyncResult>();

        // Phase 1: Fetch all API data in parallel (up to MaxConcurrentFetches at a time)
        var fetchResults = new ConcurrentDictionary<int, (List<YouTubeVideoSnapshot> Videos, YouTubePlaylistSnapshot? Meta)>();
        var semaphore = new SemaphoreSlim(MaxConcurrentFetches);
        int fetched = 0;

        var fetchTasks = playlists.Select(async playlist =>
        {
            await semaphore.WaitAsync();
            try
            {
                progress?.Report($"Fetching {Interlocked.Increment(ref fetched)}/{playlists.Count}: {playlist.Title}...");

                var videos = await youtube.GetPlaylistVideosAsync(playlist.YouTubePlaylistId);
                var meta = await youtube.GetPlaylistMetadataAsync(playlist.YouTubePlaylistId);
                fetchResults[playlist.Id] = (videos, meta);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch playlist {PlaylistId} ({Title})",
                    playlist.YouTubePlaylistId, playlist.Title);
                fetchResults[playlist.Id] = ([], null);
            }
            finally
            {
                semaphore.Release();
            }
        }).ToArray();

        await Task.WhenAll(fetchTasks);

        // Phase 2: Apply diffs sequentially (SQLite single-writer)
        var results = new Dictionary<int, SyncResult>();
        int applied = 0;

        foreach (var playlist in playlists)
        {
            progress?.Report($"Saving {++applied}/{playlists.Count}: {playlist.Title}...");

            if (!fetchResults.TryGetValue(playlist.Id, out var data) || data.Videos.Count == 0 && data.Meta is null)
            {
                results[playlist.Id] = new SyncResult(0, 0, 0);
                continue;
            }

            try
            {
                results[playlist.Id] = await ApplyPlaylistDiff(playlist, data.Videos, data.Meta);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to apply diff for playlist {PlaylistId} ({Title})",
                    playlist.YouTubePlaylistId, playlist.Title);
                results[playlist.Id] = new SyncResult(0, 0, 0);
            }
        }

        return results;
    }

    private async Task<SyncResult> ApplyPlaylistDiff(
        Playlist playlist,
        List<YouTubeVideoSnapshot> apiVideos,
        YouTubePlaylistSnapshot? metadata)
    {
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
        var newVideos = new List<Video>();
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
                // New video — collect for batch insert
                newVideos.Add(new Video
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

        // Batch insert new videos
        if (newVideos.Count > 0)
        {
            await playlistRepo.AddVideosAsync(newVideos);
            logger.LogDebug("Batch inserted {Count} new videos for {PlaylistId}", newVideos.Count, playlist.YouTubePlaylistId);
        }

        // Update playlist metadata
        if (metadata is not null)
        {
            if (playlist.Title is null || playlist.Title != metadata.Title)
                playlist.Title = metadata.Title;
            playlist.Description = metadata.Description;
            playlist.ThumbnailUrl = metadata.ThumbnailUrl;
            playlist.PublishedAt = metadata.PublishedAt;
            if (metadata.JsonMetadata is not null)
                playlist.JsonMetadata = metadata.JsonMetadata;
        }

        playlist.LastSyncedAt = DateTime.UtcNow;
        await playlistRepo.UpdateAsync(playlist);

        var result = new SyncResult(added, removed, updated);
        logger.LogInformation("Sync complete for {PlaylistId}: +{Added} -{Removed} ~{Updated}",
            playlist.YouTubePlaylistId, result.Added, result.Removed, result.Updated);

        return result;
    }
}
