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
        if (profile is null) return;

        try
        {
            var userPlaylists = await youtubeApi.GetUserPlaylistsAsync().ConfigureAwait(false);

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
                var channel = await youtubeApi.GetMyChannelAsync().ConfigureAwait(false);
                if (channel?.LikedVideosPlaylistId is not null
                    && !existingIds.Contains(channel.LikedVideosPlaylistId))
                {
                    var likedMeta = await youtubeApi.GetPlaylistMetadataAsync(channel.LikedVideosPlaylistId).ConfigureAwait(false);
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
                global::Terminal.Gui.Application.MainLoop.Invoke(() => RefreshPlaylistsAsync().GetAwaiter().GetResult());
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

    private async void OnSync()
    {
        if (_isSyncing) return;
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
        ShowSpinner($"Syncing {_selectedPlaylist.Title}...");
        try
        {
            var result = await Task.Run(() => syncService.SyncPlaylistAsync(_selectedPlaylist)).ConfigureAwait(false);
            HideSpinner();
            await RefreshVideosAsync().ConfigureAwait(false);
            MessageBox.Query("Sync Complete",
                "+" + result.Added + " added, -" + result.Removed + " removed, ~" + result.Updated + " updated", "OK");
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            HideSpinner();
            logger.LogError(ex, "YouTube API quota or auth error during sync");
            MessageBox.Query("YouTube Error", "API quota exceeded or auth expired.\nTry: ytpt login", "OK");
        }
        catch (GoogleApiException ex)
        {
            HideSpinner();
            logger.LogError(ex, "YouTube API error during sync");
            MessageBox.Query("YouTube Error", "Sync failed: " + ex.Message, "OK");
        }
        catch (HttpRequestException ex)
        {
            HideSpinner();
            logger.LogError(ex, "Network error during sync");
            MessageBox.Query("Network Error", "Could not connect to YouTube. Check your internet connection.", "OK");
        }
        catch (Exception ex)
        {
            HideSpinner();
            logger.LogError(ex, "Sync failed");
            MessageBox.Query("Error", "Sync failed: " + ex.Message, "OK");
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private async void OnSyncAll()
    {
        if (_isSyncing || _selectedProfile is null) return;

        _isSyncing = true;
        ShowSpinner("Syncing all playlists...");
        try
        {
            var syncProgress = new Progress<string>(msg =>
                global::Terminal.Gui.Application.MainLoop.Invoke(() => ShowSpinner(msg)));
            var results = await Task.Run(() => syncService.SyncAllTrackedAsync(_selectedProfile.Id, syncProgress)).ConfigureAwait(false);
            HideSpinner();
            await RefreshPlaylistsAsync().ConfigureAwait(false);
            await RefreshVideosAsync().ConfigureAwait(false);
            _videoFrame.SetNeedsDisplay();
            SetNeedsDisplay();

            int totalAdded = results.Values.Sum(r => r.Added);
            int totalRemoved = results.Values.Sum(r => r.Removed);
            MessageBox.Query("Sync All Complete",
                results.Count + " playlists synced\n+" + totalAdded + " added, -" + totalRemoved + " removed", "OK");
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            HideSpinner();
            logger.LogError(ex, "YouTube API quota or auth error during sync all");
            MessageBox.Query("YouTube Error", "API quota exceeded or auth expired.\nTry: ytpt login", "OK");
        }
        catch (GoogleApiException ex)
        {
            HideSpinner();
            logger.LogError(ex, "YouTube API error during sync all");
            MessageBox.Query("YouTube Error", "Sync failed: " + ex.Message, "OK");
        }
        catch (HttpRequestException ex)
        {
            HideSpinner();
            logger.LogError(ex, "Network error during sync all");
            MessageBox.Query("Network Error", "Could not connect to YouTube. Check your internet connection.", "OK");
        }
        catch (Exception ex)
        {
            HideSpinner();
            logger.LogError(ex, "Sync all failed");
            MessageBox.Query("Error", "Sync failed: " + ex.Message, "OK");
        }
        finally
        {
            _isSyncing = false;
        }
    }
}
