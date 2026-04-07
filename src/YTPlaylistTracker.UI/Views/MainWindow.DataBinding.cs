using System.Data;
using Microsoft.Extensions.Logging;
using Terminal.Gui;
using YTPlaylistTracker.Application.Services;
using YTPlaylistTracker.Domain.Entities;

namespace YTPlaylistTracker.UI.Views;

public partial class MainWindow
{
    private async Task RefreshVideosAsync()
    {
        try
        {
        if (_selectedPlaylist is null)
        {
            _videoTable.Table = null;
            _videoFrame.Title = "Videos";
            return;
        }

        _videos = (_showDeletedOnly
            ? await playlistRepo.GetDeletedVideosAsync(_selectedPlaylist.Id)
            : await playlistRepo.GetVideosAsync(_selectedPlaylist.Id)).ToList();

        var title = _selectedPlaylist.Title ?? _selectedPlaylist.YouTubePlaylistId;

        // Apply search filter
        var filtered = _videos;
        if (!string.IsNullOrEmpty(_searchQuery))
        {
            filtered = _videos.Where(v =>
                (v.Title ?? "").Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                (v.ChannelTitle ?? "").Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        // Apply sorting
        if (!string.IsNullOrEmpty(_sortColumn))
        {
            filtered = _sortColumn switch
            {
                "Title" => _sortAscending
                    ? filtered.OrderBy(v => v.Title).ToList()
                    : filtered.OrderByDescending(v => v.Title).ToList(),
                "Channel" => _sortAscending
                    ? filtered.OrderBy(v => v.ChannelTitle).ToList()
                    : filtered.OrderByDescending(v => v.ChannelTitle).ToList(),
                "Added Date" => _sortAscending
                    ? filtered.OrderBy(v => v.AddedAt).ToList()
                    : filtered.OrderByDescending(v => v.AddedAt).ToList(),
                "Status" => _sortAscending
                    ? filtered.OrderBy(v => v.DeletedAt.HasValue).ToList()
                    : filtered.OrderByDescending(v => v.DeletedAt.HasValue).ToList(),
                _ => filtered
            };
        }

        var dt = new DataTable();
        dt.Columns.Add("#", typeof(string));
        dt.Columns.Add("Title", typeof(string));
        dt.Columns.Add("Channel", typeof(string));
        dt.Columns.Add("Added  ", typeof(string));
        dt.Columns.Add("Status", typeof(string));

        if (filtered.Count == 0)
        {
            var hint = _selectedPlaylist.LastSyncedAt.HasValue
                ? "No videos found."
                : "Not synced yet. Press F5/s to sync.";
            dt.Rows.Add("", hint, "", "", "");
        }
        else
        {
            _lastLayout = ColumnLayout.Compute(_videoTable.Bounds.Width);

            for (int i = 0; i < filtered.Count; i++)
            {
                var v = filtered[i];
                dt.Rows.Add(
                    (i + 1).ToString(),
                    UnicodeWidth.Truncate((v.Title ?? "").Trim(), _lastLayout.TitleWidth),
                    UnicodeWidth.Truncate(v.ChannelTitle ?? "", _lastLayout.ChannelWidth),
                    (v.AddedAt?.ToString("yyyy-MM-dd") ?? "") + "  ",
                    v.DeletedAt.HasValue
                        ? "X " + (v.RemovalReason?.ToString() ?? "Removed")
                        : "Active");
            }
        }

        _filteredVideos = filtered;
        _videoTable.Table = dt;

        ApplyColumnStyles(dt);

        var sortIndicator = string.IsNullOrEmpty(_sortColumn) ? ""
            : " " + (_sortAscending ? "^" : "v") + _sortColumn;
        var syncedLabel = " | synced: " + SyncService.FormatLastSynced(_selectedPlaylist);
        _videoFrame.Title = _showDeletedOnly
            ? "Removed (" + title + ") [" + filtered.Count + "]" + sortIndicator + syncedLabel
            : "Videos (" + title + ") [" + filtered.Count + "]" + sortIndicator + syncedLabel;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh videos");
        }
    }

    private void OnVideoTableResized()
    {
        if (_videoTable.Table == null) return;

        var newLayout = ColumnLayout.Compute(_videoTable.Bounds.Width);
        if (newLayout == _lastLayout) return;

        _lastLayout = newLayout;
        var dt = _videoTable.Table;

        for (int i = 0; i < dt.Rows.Count; i++)
        {
            if (i < _filteredVideos.Count)
            {
                var v = _filteredVideos[i];
                dt.Rows[i][1] = UnicodeWidth.Truncate((v.Title ?? "").Trim(), newLayout.TitleWidth);
                dt.Rows[i][2] = UnicodeWidth.Truncate(v.ChannelTitle ?? "", newLayout.ChannelWidth);
            }
        }

        ApplyColumnStyles(dt);
        _videoTable.SetNeedsDisplay();
    }

    private void ApplyColumnStyles(DataTable dt)
    {
        _videoTable.Style.ColumnStyles.Clear();
        _videoTable.Style.ColumnStyles[dt.Columns[0]] = new TableView.ColumnStyle
            { MinWidth = _lastLayout.NumberWidth, MaxWidth = _lastLayout.NumberWidth };
        _videoTable.Style.ColumnStyles[dt.Columns[1]] = new TableView.ColumnStyle
            { MinWidth = _lastLayout.TitleWidth };
        _videoTable.Style.ColumnStyles[dt.Columns[2]] = new TableView.ColumnStyle
            { MinWidth = _lastLayout.ChannelWidth, MaxWidth = _lastLayout.ChannelWidth };
        _videoTable.Style.ColumnStyles[dt.Columns[3]] = new TableView.ColumnStyle
            { MinWidth = _lastLayout.AddedWidth, MaxWidth = _lastLayout.AddedWidth };
        _videoTable.Style.ColumnStyles[dt.Columns[4]] = new TableView.ColumnStyle
            { MinWidth = _lastLayout.StatusWidth, MaxWidth = _lastLayout.StatusWidth };
    }

    private async void OnProfileSelected(ListViewItemEventArgs e)
    {
        if (_suppressEvents) return;
        try
        {
            if (e.Item >= 0 && e.Item < _profiles.Count)
            {
                _selectedProfile = _profiles[e.Item];
                _selectedPlaylist = null;
                _videoTable.Table = null;
                _videoFrame.Title = "Videos";
                await RefreshPlaylistsAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load profile");
        }
    }

    private async void OnPlaylistSelected(ListViewItemEventArgs e)
    {
        if (_suppressEvents) return;
        try
        {
            if (e.Item >= 0 && e.Item < _playlists.Count)
            {
                _selectedPlaylist = _playlists[e.Item];
                await RefreshVideosAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load playlist");
        }
    }
}
