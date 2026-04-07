using System.Net.Http;
using Google;
using Microsoft.Extensions.Logging;
using Terminal.Gui;
using YTPlaylistTracker.Application.Helpers;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;

namespace YTPlaylistTracker.UI.Views;

public partial class MainWindow
{
    private async Task OnAddByUrlAsync()
    {
        if (_selectedProfile is null) return;

        var dialog = new Dialog("Add Playlist", 60, 8);
        var label = new Label("YouTube Playlist URL or ID:") { X = 1, Y = 1 };
        var input = new TextField("") { X = 1, Y = 2, Width = Dim.Fill(2) };
        var okBtn = new Button("Add", true);
        var cancelBtn = new Button("Cancel");

        string? inputValue = null;
        okBtn.Clicked += () => { inputValue = input.Text?.ToString(); global::Terminal.Gui.Application.RequestStop(); };
        cancelBtn.Clicked += () => global::Terminal.Gui.Application.RequestStop();

        dialog.Add(label, input);
        dialog.AddButton(okBtn);
        dialog.AddButton(cancelBtn);
        global::Terminal.Gui.Application.Run(dialog);

        if (string.IsNullOrWhiteSpace(inputValue)) return;

        var playlistId = PlaylistUrlParser.ExtractPlaylistId(inputValue);
        logger.LogInformation("Adding playlist: {PlaylistId}", playlistId);

        var playlist = new Playlist
        {
            Profile = _selectedProfile,
            ProfileId = _selectedProfile.Id,
            YouTubePlaylistId = playlistId,
            IsTracked = true
        };

        try
        {
            var meta = await youtubeApi.GetPlaylistMetadataAsync(playlistId);
            if (meta is not null)
            {
                playlist.Title = meta.Title;
                playlist.Description = meta.Description;
                playlist.ThumbnailUrl = meta.ThumbnailUrl;
                playlist.PublishedAt = meta.PublishedAt;
                playlist.JsonMetadata = meta.JsonMetadata;
            }

            await playlistRepo.AddAsync(playlist);
            await RefreshPlaylistsAsync();
            MessageBox.Query("Success", "Added playlist: " + (playlist.Title ?? playlistId), "OK");
        }
        catch (GoogleApiException ex)
        {
            logger.LogError(ex, "YouTube API error adding playlist");
            MessageBox.Query("YouTube Error", "Could not fetch playlist: " + ex.Message, "OK");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Network error adding playlist");
            MessageBox.Query("Network Error", "Could not connect to YouTube. Check your internet connection.", "OK");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add playlist");
            MessageBox.Query("Error", "Failed to add playlist: " + ex.Message, "OK");
        }
    }

    private async void OnToggleTrack()
    {
        if (_selectedPlaylist is null) return;

        // Warn when enabling tracking on Liked Videos (can be very large)
        if (!_selectedPlaylist.IsTracked
            && _selectedPlaylist.YouTubePlaylistId.StartsWith("LL", StringComparison.Ordinal))
        {
            var result = MessageBox.Query("Large Playlist",
                "Liked Videos can contain thousands of videos.\n" +
                "Initial sync may take a while.\n\n" +
                "Enable tracking?", "Yes", "Cancel");
            if (result != 0) return;
        }

        try
        {
            _selectedPlaylist.IsTracked = !_selectedPlaylist.IsTracked;
            await playlistRepo.UpdateAsync(_selectedPlaylist);
            await RefreshPlaylistsAsync();
        }
        catch (Exception ex)
        {
            _selectedPlaylist.IsTracked = !_selectedPlaylist.IsTracked; // revert
            logger.LogError(ex, "Failed to toggle tracking");
            MessageBox.Query("Error", "Failed to update: " + ex.Message, "OK");
        }
    }

    private async void OnToggleAllTracking()
    {
        if (_selectedProfile is null || _playlists.Count == 0) return;
        try
        {
            // If any are untracked, track all. Otherwise untrack all.
            bool newState = _playlists.Any(p => !p.IsTracked);
            foreach (var p in _playlists)
            {
                p.IsTracked = newState;
                await playlistRepo.UpdateAsync(p);
            }
            await RefreshPlaylistsAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to toggle all tracking");
            MessageBox.Query("Error", "Failed to update: " + ex.Message, "OK");
        }
    }

    private async void OnToggleDeleted()
    {
        _showDeletedOnly = !_showDeletedOnly;
        try { await RefreshVideosAsync(); }
        catch (Exception ex) { logger.LogError(ex, "Failed to toggle deleted view"); }
    }

    private async void ShowDetail()
    {
        try
        {
            if (_profileList.HasFocus && _selectedProfile is not null)
            {
                var playlists = await playlistRepo.GetByProfileAsync(_selectedProfile.Id);
                var tracked = playlists.Count(p => p.IsTracked);
                var dialog = DetailDialog.ForProfile(_selectedProfile, playlists.Count, tracked, browser);
                global::Terminal.Gui.Application.Run(dialog);
            }
            else if (_playlistList.HasFocus && _selectedPlaylist is not null)
            {
                var videos = await playlistRepo.GetVideosAsync(_selectedPlaylist.Id);
                var active = videos.Count(v => v.DeletedAt == null);
                var removed = videos.Count(v => v.DeletedAt != null);
                var dialog = DetailDialog.ForPlaylist(_selectedPlaylist, active, removed, browser);
                global::Terminal.Gui.Application.Run(dialog);
            }
            else if (_videoTable.HasFocus && _videoTable.SelectedRow >= 0 && _videoTable.SelectedRow < _filteredVideos.Count)
            {
                var video = _filteredVideos[_videoTable.SelectedRow];
                var dialog = DetailDialog.ForVideo(video, browser);
                global::Terminal.Gui.Application.Run(dialog);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to show details");
        }
    }

    private void ShowSortMenu()
    {
        var dialog = new Dialog("Sort by", 30, 10);
        string[] options = ["Title", "Channel", "Added Date", "Status"];
        var list = new ListView(options)
        {
            Width = Dim.Fill(), Height = Dim.Fill()
        };
        list.OpenSelectedItem += (args) =>
        {
            var selected = options[args.Item];
            if (_sortColumn == selected)
                _sortAscending = !_sortAscending;
            else
            {
                _sortColumn = selected;
                _sortAscending = true;
            }
            global::Terminal.Gui.Application.RequestStop();
        };
        dialog.Add(list);
        var cancelBtn2 = new Button("Cancel");
        cancelBtn2.Clicked += () => global::Terminal.Gui.Application.RequestStop();
        dialog.AddButton(cancelBtn2);
        global::Terminal.Gui.Application.Run(dialog);
        _ = RefreshVideosAsync();
    }

    private void ShowSearch()
    {
        if (_searchField is not null) return;

        _searchField = new TextField("")
        {
            X = 0, Y = 0,
            Width = Dim.Fill(),
        };
        _searchField.TextChanged += (unused) =>
        {
            _searchQuery = _searchField.Text?.ToString() ?? "";
            _ = RefreshVideosAsync();
        };
        _searchField.KeyPress += (e) =>
        {
            if (e.KeyEvent.Key == Key.Esc)
            {
                HideSearch();
                e.Handled = true;
            }
            else if (e.KeyEvent.Key == Key.Enter)
            {
                _videoTable.SetFocus();
                e.Handled = true;
            }
        };

        _videoFrame.Add(_searchField);
        _videoTable.Y = 1;
        _videoTable.Height = Dim.Fill();
        _searchField.SetFocus();
        _videoFrame.SetNeedsDisplay();
    }

    private void HideSearch()
    {
        if (_searchField is null) return;
        _videoFrame.Remove(_searchField);
        _searchField = null;
        _searchQuery = "";
        _videoTable.Y = 0;
        _videoTable.Height = Dim.Fill();
        _videoTable.SetFocus();
        _ = RefreshVideosAsync();
    }
}
