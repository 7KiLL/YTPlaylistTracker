using System.Net.Http;
using Google;
using Microsoft.Extensions.Logging;
using Terminal.Gui;
using YTPlaylistTracker.Application.Services;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Domain.Models;

namespace YTPlaylistTracker.UI.Views;

public partial class MainWindow
{
    private async Task FetchAndImportAllPlaylists(Profile? profile)
    {
        if (profile is null || _youtubeApi is null) return;

        try
        {
            var userPlaylists = await _youtubeApi.GetUserPlaylistsAsync().ConfigureAwait(false);

            var dbPlaylists = await playlistRepo.GetByProfileAsync(profile.Id).ConfigureAwait(false);
            var existingIds = dbPlaylists.Select(p => p.YouTubePlaylistId).ToHashSet(StringComparer.Ordinal);

            var newPlaylists = new List<Playlist>();
            foreach (var meta in userPlaylists)
            {
                if (existingIds.Contains(meta.PlaylistId)) continue;

                newPlaylists.Add(new Playlist
                {
                    Profile = profile,
                    ProfileId = profile.Id,
                    YouTubePlaylistId = meta.PlaylistId,
                    Title = meta.Title,

                    IsTracked = false,
                    Description = meta.Description,
                    ThumbnailUrl = meta.ThumbnailUrl,
                    PublishedAt = meta.PublishedAt,
                    JsonMetadata = meta.JsonMetadata,
                });
            }

            // Import Liked Videos playlist if not already in DB
            try
            {
                var channel = await _youtubeApi!.GetMyChannelAsync().ConfigureAwait(false);
                if (channel?.LikedVideosPlaylistId is not null
                    && !existingIds.Contains(channel.LikedVideosPlaylistId))
                {
                    var likedMeta = await _youtubeApi!.GetPlaylistMetadataAsync(channel.LikedVideosPlaylistId).ConfigureAwait(false);
                    if (likedMeta is not null)
                    {
                        newPlaylists.Add(new Playlist
                        {
                            Profile = profile,
                            ProfileId = profile.Id,
                            YouTubePlaylistId = channel.LikedVideosPlaylistId,
                            Title = likedMeta.Title ?? "Liked Videos",

                            IsTracked = false,
                            Description = likedMeta.Description,
                            ThumbnailUrl = likedMeta.ThumbnailUrl,
                            PublishedAt = likedMeta.PublishedAt,
                            JsonMetadata = likedMeta.JsonMetadata,
                        });
                        logger.LogInformation("Imported Liked Videos playlist ({Id})", channel.LikedVideosPlaylistId);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to import Liked Videos playlist");
            }

            if (newPlaylists.Count > 0)
            {
                await playlistRepo.AddPlaylistsAsync(newPlaylists).ConfigureAwait(false);
                logger.LogInformation("Imported {Count} new playlists from YouTube", newPlaylists.Count);
                InvokeUI(() => RefreshPlaylistsAsync().GetAwaiter().GetResult());
            }
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            logger.LogWarning("YouTube API quota/auth issue during background fetch — skipping");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch playlists from YouTube");
        }
    }

    private void OnSync()
    {
        if (_isSyncing)
        {
            MessageBox.Query("Sync in Progress", "A sync is already running.\nWait for it to finish first.", "OK");
            return;
        }
        if (_youtubeApi is null)
        {
            MessageBox.Query("Not Logged In", "This profile is not authenticated.\nPress L to login first.", "OK");
            return;
        }
        if (_selectedPlaylist is null)
        {
            MessageBox.Query("Info", "Select a playlist first.", "OK");
            return;
        }

        var remaining = SyncService.GetRemainingCooldown(_selectedPlaylist);
        if (remaining.HasValue)
        {
            var r = remaining.Value;
            var timeLeft = r.TotalHours >= 1 ? $"{(int)r.TotalHours}h {r.Minutes}m" : $"{r.Minutes}m";
            var title = _selectedPlaylist.Title ?? _selectedPlaylist.YouTubePlaylistId;
            MessageBox.Query("Cooldown",
                $"{title} can only be synced once per day.\n" +
                $"Last synced: {SyncService.FormatLastSynced(_selectedPlaylist)}\n" +
                $"Next sync available in {timeLeft}.", "OK");
            return;
        }

        _isSyncing = true;
        var playlist = _selectedPlaylist;
        var youtube = _youtubeApi;
        ShowSpinner($"Syncing {playlist.Title}...");

        _ = Task.Run(async () =>
        {
            try
            {
                var syncProgress = new Progress<string>(msg =>
                    global::Terminal.Gui.Application.Invoke(() => ShowSpinner(msg)));
                var result = await syncService.SyncPlaylistAsync(playlist, youtube, syncProgress).ConfigureAwait(false);
                InvokeUI(() =>
                {
                    HideSpinner();
                    RefreshVideosAsync().GetAwaiter().GetResult();
                    MessageBox.Query("Sync Complete",
                        "+" + result.Added + " added, -" + result.Removed + " removed, ~" + result.Updated + " updated", "OK");
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sync failed for {PlaylistId}", playlist.YouTubePlaylistId);
                InvokeUI(() =>
                {
                    HideSpinner();
                    MessageBox.Query("Sync Error", "Sync failed: " + ex.Message, "OK");
                });
            }
            finally { _isSyncing = false; }
        });
    }

    private void OnSyncAll()
    {
        if (_isSyncing)
        {
            MessageBox.Query("Sync in Progress", "A sync is already running.\nWait for it to finish first.", "OK");
            return;
        }
        if (_selectedProfile is null) return;
        if (_youtubeApi is null)
        {
            MessageBox.Query("Not Logged In", "This profile is not authenticated.\nPress L to login first.", "OK");
            return;
        }

        _isSyncing = true;
        var profileId = _selectedProfile.Id;
        var youtube = _youtubeApi;
        ShowSpinner("Syncing all playlists...");

        _ = Task.Run(async () =>
        {
            try
            {
                var syncProgress = new Progress<string>(msg =>
                    global::Terminal.Gui.Application.Invoke(() => ShowSpinner(msg)));
                var results = await syncService.SyncAllTrackedAsync(profileId, youtube, syncProgress).ConfigureAwait(false);
                int totalAdded = results.Values.Sum(r => r.Added);
                int totalRemoved = results.Values.Sum(r => r.Removed);
                InvokeUI(() =>
                {
                    HideSpinner();
                    RefreshPlaylistsAsync().GetAwaiter().GetResult();
                    RefreshVideosAsync().GetAwaiter().GetResult();
                    SetNeedsDraw();
                    MessageBox.Query("Sync All Complete",
                        results.Count + " playlists synced\n+" + totalAdded + " added, -" + totalRemoved + " removed", "OK");
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sync all failed");
                InvokeUI(() =>
                {
                    HideSpinner();
                    MessageBox.Query("Sync Error", "Sync failed: " + ex.Message, "OK");
                });
            }
            finally { _isSyncing = false; }
        });
    }
}
