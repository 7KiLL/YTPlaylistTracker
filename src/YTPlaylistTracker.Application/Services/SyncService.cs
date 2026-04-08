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
    public static readonly TimeSpan AutoSyncCooldown = TimeSpan.FromHours(6);
    private const int MaxConcurrentFetches = 4;

    public static TimeSpan? GetRemainingCooldown(Playlist playlist)
    {
        var policy = PlaylistPolicy.For(playlist.Kind);
        if (policy.ManualCooldown is not { } cooldown || playlist.LastSyncedAt is null)
            return null;
        var remaining = cooldown - (DateTime.UtcNow - playlist.LastSyncedAt.Value);
        return remaining > TimeSpan.Zero ? remaining : null;
    }

    public static string FormatLastSynced(Playlist playlist)
    {
        if (playlist.LastSyncedAt is null)
            return "never";
        var ago = DateTime.UtcNow - playlist.LastSyncedAt.Value;
        return ago.TotalMinutes < 1 ? "just now"
            : ago.TotalMinutes < 60 ? $"{(int)ago.TotalMinutes}m ago"
            : ago.TotalHours < 24 ? $"{(int)ago.TotalHours}h ago"
            : $"{(int)ago.TotalDays}d ago";
    }

    private static bool IsAutoSyncCooldownActive(Playlist playlist)
    {
        if (playlist.LastSyncedAt is null) return false;
        var policy = PlaylistPolicy.For(playlist.Kind);
        if (!policy.AllowAutoSync) return true;
        return DateTime.UtcNow - playlist.LastSyncedAt.Value < AutoSyncCooldown;
    }

    // --- Sync operations ---

    public async Task<SyncResult> SyncPlaylistAsync(Playlist playlist)
    {
        logger.LogInformation("Syncing playlist: {PlaylistId} ({Title})", playlist.YouTubePlaylistId, playlist.Title);

        var apiVideos = await youtube.GetPlaylistVideosAsync(playlist.YouTubePlaylistId).ConfigureAwait(false);
        var metadata = await youtube.GetPlaylistMetadataAsync(playlist.YouTubePlaylistId).ConfigureAwait(false);

        return await ApplyPlaylistDiff(playlist, apiVideos, metadata).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<int, SyncResult>> SyncAllTrackedAsync(int profileId, IProgress<string>? progress = null)
    {
        var allPlaylists = await playlistRepo.GetTrackedByProfileAsync(profileId).ConfigureAwait(false);

        // Filter out playlists on cooldown (Liked never auto-syncs, others skip if <6h)
        List<Playlist> playlists = [..allPlaylists.Where(p => !IsAutoSyncCooldownActive(p))];
        var skipped = allPlaylists.Count - playlists.Count;

        if (skipped > 0)
            logger.LogInformation("Auto-sync: skipping {Skipped} playlists on cooldown", skipped);

        logger.LogInformation("Syncing {Count} tracked playlists for profile {ProfileId}", playlists.Count, profileId);

        if (playlists.Count == 0)
            return new Dictionary<int, SyncResult>();

        // Phase 1: Fetch all API data in parallel (up to MaxConcurrentFetches at a time)
        var fetchResults = new ConcurrentDictionary<int, (IReadOnlyList<YouTubeVideoSnapshot> Videos, YouTubePlaylistSnapshot? Meta)>();
        using var semaphore = new SemaphoreSlim(MaxConcurrentFetches);
        int fetched = 0;

        var fetchTasks = playlists.Select(async playlist =>
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                progress?.Report($"Fetching {Interlocked.Increment(ref fetched)}/{playlists.Count}: {playlist.Title}...");

                var videos = await youtube.GetPlaylistVideosAsync(playlist.YouTubePlaylistId).ConfigureAwait(false);
                var meta = await youtube.GetPlaylistMetadataAsync(playlist.YouTubePlaylistId).ConfigureAwait(false);
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

        await Task.WhenAll(fetchTasks).ConfigureAwait(false);

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
                results[playlist.Id] = await ApplyPlaylistDiff(playlist, data.Videos, data.Meta).ConfigureAwait(false);
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
        IReadOnlyList<YouTubeVideoSnapshot> apiVideos,
        YouTubePlaylistSnapshot? metadata)
    {
        // YouTube allows duplicate videos in a playlist — deduplicate, keeping first occurrence
        var apiVideoIds = apiVideos.DistinctBy(v => v.VideoId).ToDictionary(v => v.VideoId, StringComparer.Ordinal);

        logger.LogDebug("API returned {Count} videos for playlist {PlaylistId}", apiVideos.Count, playlist.YouTubePlaylistId);

        var dbVideos = await playlistRepo.GetVideosAsync(playlist.Id).ConfigureAwait(false);
        List<Video> activeDbVideos = [.. dbVideos.Where(v => v.DeletedAt is null)];
        var deletedDbVideos = dbVideos.Where(v => v.DeletedAt is not null).ToDictionary(v => v.YouTubeVideoId, StringComparer.Ordinal);
        var activeDbVideoIds = activeDbVideos.ToDictionary(v => v.YouTubeVideoId, StringComparer.Ordinal);

        int removed = await DetectRemovals(activeDbVideos, apiVideoIds).ConfigureAwait(false);
        var (added, updated, newVideos) = await DetectAdditionsAndUpdates(
            apiVideoIds, activeDbVideoIds, deletedDbVideos, playlist).ConfigureAwait(false);

        if (newVideos.Count > 0)
        {
            await playlistRepo.AddVideosAsync(newVideos).ConfigureAwait(false);
            logger.LogDebug("Batch inserted {Count} new videos for {PlaylistId}", newVideos.Count, playlist.YouTubePlaylistId);
        }

        UpdatePlaylistMetadata(playlist, metadata);
        playlist.LastSyncedAt = DateTime.UtcNow;
        await playlistRepo.UpdateAsync(playlist).ConfigureAwait(false);

        var result = new SyncResult(added, removed, updated);
        logger.LogInformation("Sync complete for {PlaylistId}: +{Added} -{Removed} ~{Updated}",
            playlist.YouTubePlaylistId, result.Added, result.Removed, result.Updated);

        return result;
    }

    private async Task<int> DetectRemovals(
        List<Video> activeDbVideos,
        Dictionary<string, YouTubeVideoSnapshot> apiVideoIds)
    {
        int removed = 0;
        foreach (var dbVideo in activeDbVideos)
        {
            if (!apiVideoIds.ContainsKey(dbVideo.YouTubeVideoId))
            {
                logger.LogWarning("Video removed: {VideoId} ({Title})", dbVideo.YouTubeVideoId, dbVideo.Title);
                dbVideo.DeletedAt = DateTime.UtcNow;
                dbVideo.RemovalReason = await youtube.CheckVideoStatusAsync(dbVideo.YouTubeVideoId).ConfigureAwait(false);
                await playlistRepo.UpdateVideoAsync(dbVideo).ConfigureAwait(false);
                removed++;
            }
        }
        return removed;
    }

    private async Task<(int Added, int Updated, List<Video> NewVideos)> DetectAdditionsAndUpdates(
        Dictionary<string, YouTubeVideoSnapshot> apiVideoIds,
        Dictionary<string, Video> activeDbVideoIds,
        Dictionary<string, Video> deletedDbVideos,
        Playlist playlist)
    {
        int added = 0, updated = 0;
        var newVideos = new List<Video>();

        foreach (var apiVideo in apiVideoIds.Values)
        {
            if (activeDbVideoIds.TryGetValue(apiVideo.VideoId, out var dbVideo))
            {
                if (UpdateVideoFields(dbVideo, apiVideo))
                {
                    await playlistRepo.UpdateVideoAsync(dbVideo).ConfigureAwait(false);
                    updated++;
                }
            }
            else if (deletedDbVideos.TryGetValue(apiVideo.VideoId, out var deletedVideo))
            {
                logger.LogInformation("Video re-added: {VideoId} ({Title})", apiVideo.VideoId, apiVideo.Title);
                ReactivateVideo(deletedVideo, apiVideo);
                await playlistRepo.UpdateVideoAsync(deletedVideo).ConfigureAwait(false);
                added++;
            }
            else
            {
                newVideos.Add(CreateVideo(playlist, apiVideo));
                added++;
            }
        }

        return (added, updated, newVideos);
    }

    private bool UpdateVideoFields(Video video, YouTubeVideoSnapshot snapshot)
    {
        var before = (video.Title, video.ChannelTitle, video.Description,
            video.ThumbnailUrl, video.Position, video.JsonMetadata);

        video.Title = snapshot.Title;
        video.ChannelTitle = snapshot.ChannelTitle;
        video.Description = snapshot.Description;
        video.ThumbnailUrl = snapshot.ThumbnailUrl;
        video.Position = snapshot.Position;
        video.JsonMetadata = snapshot.JsonMetadata;

        var after = (video.Title, video.ChannelTitle, video.Description,
            video.ThumbnailUrl, video.Position, video.JsonMetadata);

        if (before == after)
            return false;

        if (before.Title != after.Title)
            logger.LogDebug("Video title changed: {Old} → {New}", before.Title, after.Title);

        return true;
    }

    private static void ReactivateVideo(Video video, YouTubeVideoSnapshot apiVideo)
    {
        video.DeletedAt = null;
        video.RemovalReason = null;
        video.Title = apiVideo.Title;
        video.ChannelTitle = apiVideo.ChannelTitle;
        video.Description = apiVideo.Description;
        video.ThumbnailUrl = apiVideo.ThumbnailUrl;
        video.Position = apiVideo.Position;
        video.JsonMetadata = apiVideo.JsonMetadata;
        video.AddedAt = apiVideo.AddedAt;
    }

    private static Video CreateVideo(Playlist playlist, YouTubeVideoSnapshot apiVideo) => new()
    {
        Playlist = playlist,
        PlaylistId = playlist.Id,
        YouTubeVideoId = apiVideo.VideoId,
        Title = apiVideo.Title,
        ChannelTitle = apiVideo.ChannelTitle,
        AddedAt = apiVideo.AddedAt,
        Description = apiVideo.Description,
        ThumbnailUrl = apiVideo.ThumbnailUrl,
        Position = apiVideo.Position,
        JsonMetadata = apiVideo.JsonMetadata,
    };

    private static void UpdatePlaylistMetadata(Playlist playlist, YouTubePlaylistSnapshot? metadata)
    {
        if (metadata is null) return;

        if (playlist.Title is null || !string.Equals(playlist.Title, metadata.Title, StringComparison.Ordinal))
            playlist.Title = metadata.Title;
        playlist.Description = metadata.Description;
        playlist.ThumbnailUrl = metadata.ThumbnailUrl;
        playlist.PublishedAt = metadata.PublishedAt;
        if (metadata.JsonMetadata is not null)
            playlist.JsonMetadata = metadata.JsonMetadata;
    }
}
