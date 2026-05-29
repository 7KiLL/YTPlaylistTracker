using System.Net.Http;
using Google;
using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Application.Helpers;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Domain.Models;

namespace YTPlaylistTracker.UI.Views;

public partial class MainWindow
{
    private async Task OnAddByUrlAsync()
    {
        if (_selectedProfile is null) return;

        var inputValue = Dialogs.PromptForText(this, "Add Playlist", "Add",
            description: "YouTube Playlist URL or ID:", width: 60, height: 9);
        if (string.IsNullOrWhiteSpace(inputValue)) return;

        var playlistId = PlaylistUrlParser.ExtractPlaylistId(inputValue);
        logger.LogInformation("Adding playlist: {PlaylistId}", playlistId);

        var playlist = new Playlist
        {
            Profile = _selectedProfile,
            ProfileId = _selectedProfile.Id,
            YouTubePlaylistId = playlistId,
            IsManuallyAdded = true,
            IsTracked = true,
        };

        try
        {
            var meta = _youtubeApi is not null
                ? await _youtubeApi.GetPlaylistMetadataAsync(playlistId).ConfigureAwait(false)
                : null;
            if (meta is not null)
            {
                playlist.Title = meta.Title;
                playlist.Description = meta.Description;
                playlist.ThumbnailUrl = meta.ThumbnailUrl;
                playlist.PublishedAt = meta.PublishedAt;
                playlist.JsonMetadata = meta.JsonMetadata;
            }

            await playlistRepo.AddAsync(playlist).ConfigureAwait(false);
            await RefreshPlaylistsAsync().ConfigureAwait(false);
            Dialogs.Query(_app, "Success", "Added playlist: " + (playlist.Title ?? playlistId), "OK");
        }
        catch (GoogleApiException ex)
        {
            logger.LogError(ex, "YouTube API error adding playlist");
            Dialogs.Query(_app, "YouTube Error", "Could not fetch playlist: " + ex.Message, "OK");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Network error adding playlist");
            Dialogs.Query(_app, "Network Error", "Could not connect to YouTube. Check your internet connection.", "OK");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add playlist");
            Dialogs.Query(_app, "Error", "Failed to add playlist: " + ex.Message, "OK");
        }
    }

    private async void OnToggleTrack()
    {
        if (_selectedPlaylist is null) return;

        var policy = PlaylistPolicy.For(_selectedPlaylist.Kind);
        if (!policy.AllowAutoSync) return;
        if (!_selectedPlaylist.IsTracked && policy.TrackingWarning is not null)
        {
            var confirmed = false;
            var dialog = new Dialog() { Title = "", Width = 56, Height = 12 };
            dialog.Border!.Settings &= ~BorderSettings.Title;
            dialog.Add(new Label { Text = " Large Playlist", X = 0, Y = 0, Width = Dim.Fill(), SchemeName = Theme.SchemeFrame });
            dialog.Add(new Label()
            {
                Text = policy.TrackingWarning,
                X = 1, Y = 2,
                Width = Dim.Fill(1),
                Height = 4,
            });
            var yesBtn = new Button() { Text = "Enable Tracking", IsDefault = true };
            yesBtn.Accepting += (s, e) => { confirmed = true; _app.RequestStop(); };
            var cancelBtn = new Button() { Text = "Cancel" };
            cancelBtn.Accepting += (s, e) => _app.RequestStop();
            dialog.AddButton(yesBtn);
            dialog.AddButton(cancelBtn);
            _app.Run(dialog);
            if (!confirmed) return;
        }

        try
        {
            _selectedPlaylist.IsTracked = !_selectedPlaylist.IsTracked;
            await playlistRepo.UpdateAsync(_selectedPlaylist).ConfigureAwait(false);
            await RefreshPlaylistsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _selectedPlaylist.IsTracked = !_selectedPlaylist.IsTracked; // revert
            logger.LogError(ex, "Failed to toggle tracking");
            Dialogs.Query(_app, "Error", "Failed to update: " + ex.Message, "OK");
        }
    }

    private async void OnToggleAllTracking()
    {
        if (_selectedProfile is null || _playlists.Count == 0) return;
        try
        {
            // If any trackable are untracked, track all. Otherwise untrack all.
            var trackable = _playlists.Where(p => PlaylistPolicy.For(p.Kind).AllowAutoSync).ToList();
            if (trackable.Count == 0) return;
            bool newState = trackable.Exists(p => !p.IsTracked);
            foreach (var p in trackable)
            {
                p.IsTracked = newState;
                await playlistRepo.UpdateAsync(p).ConfigureAwait(false);
            }
            await RefreshPlaylistsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to toggle all tracking");
            Dialogs.Query(_app, "Error", "Failed to update: " + ex.Message, "OK");
        }
    }

    private async void OnToggleDeleted()
    {
        _showDeletedOnly = !_showDeletedOnly;
        try { await RefreshVideosAsync().ConfigureAwait(false); }
        catch (Exception ex) { logger.LogError(ex, "Failed to toggle deleted view"); }
    }

    private async void ShowDetail()
    {
        try
        {
            if (_profileList.HasFocus && _selectedProfile is not null)
            {
                var playlists = await playlistRepo.GetByProfileAsync(_selectedProfile.Id).ConfigureAwait(false);
                var tracked = playlists.Count(p => p.IsTracked);
                var dialog = DetailDialog.ForProfile(_selectedProfile, playlists.Count, tracked, browser);
                _app.Run(dialog);
            }
            else if (_playlistList.HasFocus && _selectedPlaylist is not null)
            {
                var videos = await playlistRepo.GetVideosAsync(_selectedPlaylist.Id).ConfigureAwait(false);
                var active = videos.Count(v => v.DeletedAt == null);
                var removed = videos.Count(v => v.DeletedAt != null);
                var dialog = DetailDialog.ForPlaylist(_selectedPlaylist, active, removed, browser);
                _app.Run(dialog);
            }
            else if (_videoTable.HasFocus && _videoTable.Value?.SelectedCell.Y is int row && row >= 0 && row < _filteredVideos.Count)
            {
                var video = _filteredVideos[row];
                var dialog = DetailDialog.ForVideo(video, browser);
                _app.Run(dialog);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to show details");
        }
    }

    private void ShowSortMenu()
    {
        var dialog = new Dialog() { Title = "", Width = 30, Height = 11 };
        dialog.Border!.Settings &= ~BorderSettings.Title;
        dialog.Add(new Label { Text = " Sort by", X = 0, Y = 0, Width = Dim.Fill(), SchemeName = Theme.SchemeFrame });
        string[] options = ["Title", "Channel", "Added Date", "Status"];
        var list = new ListView()
        {
            Y = 1, Width = Dim.Fill(), Height = Dim.Fill(),
        };
        list.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(options));
        list.Accepting += (sender, e) =>
        {
            var selected = options[list.SelectedItem ?? 0];
            if (string.Equals(_sortColumn, selected, StringComparison.Ordinal))
                _sortAscending = !_sortAscending;
            else
            {
                _sortColumn = selected;
                _sortAscending = true;
            }
            _app.RequestStop();
        };
        dialog.Add(list);
        var cancelBtn2 = new Button() { Text = "Cancel" };
        cancelBtn2.Accepting += (sender, e) => _app.RequestStop();
        dialog.AddButton(cancelBtn2);
        _app.Run(dialog);
        ApplyFilterAndSort();
    }

    private void ShowSearch()
    {
        if (_searchField is not null) return;

        _searchField = new TextField()
        {
            Text = "",
            X = 1, Y = 1,
            Width = Dim.Fill(1),
        };
        _searchField.TextChanged += (sender, e) =>
        {
            _searchQuery = _searchField.Text ?? "";

            if (_searchDebounceTimer is not null)
                _app.RemoveTimeout(_searchDebounceTimer);

            _searchDebounceTimer = _app.AddTimeout(
                TimeSpan.FromMilliseconds(150), () =>
                {
                    ApplyFilterAndSort();
                    return false;
                });
        };
        _searchField.KeyDown += (sender, e) =>
        {
            if (e == Key.Backspace.WithCtrl)
            {
                var text = _searchField.Text ?? "";
                var pos = _searchField.InsertionPoint;
                if (pos > 0)
                {
                    int end = pos;
                    while (end > 0 && char.IsWhiteSpace(text[end - 1])) end--;
                    while (end > 0 && !char.IsWhiteSpace(text[end - 1])) end--;
                    _searchField.Text = text.Remove(end, pos - end);
                    _searchField.InsertionPoint = end;
                }
                e.Handled = true;
            }
            else if (e == Key.Esc)
            {
                HideSearch();
                e.Handled = true;
            }
            else if (e == Key.Enter)
            {
                _videoTable.SetFocus();
                e.Handled = true;
            }
        };

        _videoFrame.Add(_searchField);
        _videoTable.Y = 2;
        _videoTable.Height = Dim.Fill();
        _searchField.SetFocus();
        _videoFrame.SetNeedsDraw();
    }

    private void HideSearch()
    {
        if (_searchField is null) return;

        if (_searchDebounceTimer is not null)
        {
            _app.RemoveTimeout(_searchDebounceTimer);
            _searchDebounceTimer = null;
        }

        _videoFrame.Remove(_searchField);
        _searchField = null;
        _searchQuery = "";
        _videoTable.Y = 1;
        _videoTable.Height = Dim.Fill();
        _videoTable.SetFocus();
        ApplyFilterAndSort();
    }
}
