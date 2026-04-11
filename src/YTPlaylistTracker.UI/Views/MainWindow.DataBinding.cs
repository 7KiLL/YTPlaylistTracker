using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Application.Services;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;

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
                _videoStatusLabel.Text = " Videos";
                return;
            }

            _videos = (_showDeletedOnly
                ? await playlistRepo.GetDeletedVideosAsync(_selectedPlaylist.Id)
.ConfigureAwait(false) : await playlistRepo.GetVideosAsync(_selectedPlaylist.Id).ConfigureAwait(false)).ToList();

            ApplyFilterAndSort();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh videos");
        }
    }

    private void ApplyFilterAndSort()
    {
        if (_selectedPlaylist is null) return;

        var title = _selectedPlaylist.Title ?? _selectedPlaylist.YouTubePlaylistId;

        var filtered = _videos;
        if (!string.IsNullOrEmpty(_searchQuery))
        {
            filtered = _videos.Where(v =>
                (v.Title ?? "").Contains(_searchQuery, StringComparison.OrdinalIgnoreCase) ||
                (v.ChannelTitle ?? "").Contains(_searchQuery, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        if (!string.IsNullOrEmpty(_sortColumn))
        {
            filtered = _sortColumn switch
            {
                "Title" => _sortAscending
                    ? filtered.OrderBy(v => v.Title, StringComparer.Ordinal).ToList()
                    : filtered.OrderByDescending(v => v.Title, StringComparer.Ordinal).ToList(),
                "Channel" => _sortAscending
                    ? filtered.OrderBy(v => v.ChannelTitle, StringComparer.Ordinal).ToList()
                    : filtered.OrderByDescending(v => v.ChannelTitle, StringComparer.Ordinal).ToList(),
                "Added Date" => _sortAscending
                    ? filtered.OrderBy(v => v.AddedAt).ToList()
                    : filtered.OrderByDescending(v => v.AddedAt).ToList(),
                "Status" => _sortAscending
                    ? filtered.OrderBy(v => v.DeletedAt.HasValue).ToList()
                    : filtered.OrderByDescending(v => v.DeletedAt.HasValue).ToList(),
                _ => filtered,
            };
        }

        string Arrow(string col) => string.Equals(_sortColumn, col, StringComparison.Ordinal) ? (_sortAscending ? Glyphs.SortAscending : Glyphs.SortDescending) : "";

        var dt = new DataTable();
        dt.Columns.Add("#", typeof(string));
        dt.Columns.Add("Title" + Arrow("Title"), typeof(string));
        dt.Columns.Add("Channel" + Arrow("Channel"), typeof(string));
        dt.Columns.Add("Added" + Arrow("Added Date"), typeof(string));
        dt.Columns.Add("Status" + Arrow("Status"), typeof(string));

        if (filtered.Count == 0)
        {
            var hint = _selectedPlaylist.LastSyncedAt.HasValue
                ? "No videos found."
                : "Not synced yet. Press F5/s to sync.";
            dt.Rows.Add("", hint, "", "", "");
        }
        else
        {
            _lastLayout = ColumnLayout.Compute(_videoTable.Viewport.Width);

            for (int i = 0; i < filtered.Count; i++)
            {
                var v = filtered[i];
                dt.Rows.Add(
                    (i + 1).ToString(),
                    UnicodeWidth.Truncate((v.Title ?? "").Trim(), _lastLayout.TitleWidth),
                    UnicodeWidth.Truncate(v.ChannelTitle ?? "", _lastLayout.ChannelWidth),
                    (v.AddedAt?.ToString("yyyy-MM-dd") ?? "") + "  ",
                    v.DeletedAt.HasValue
                        ? Glyphs.StatusRemoved + (v.RemovalReason?.ToString() ?? Glyphs.StatusRemovedFallback)
                        : Glyphs.StatusActive);
            }
        }

        _filteredVideos = filtered;
        _videoTable.Table = new DataTableSource(dt);
        ApplyColumnStyles(dt);

        var syncedLabel = " | synced: " + SyncService.FormatLastSynced(_selectedPlaylist);
        _videoStatusLabel.Text = _showDeletedOnly
            ? $" Removed ({title}) [{filtered.Count}]{syncedLabel}"
            : $" Videos ({title}) [{filtered.Count}]{syncedLabel}";
    }

    private void OnVideoTableResized()
    {
        if (_videoTable.Table == null) return;

        var newLayout = ColumnLayout.Compute(_videoTable.Viewport.Width);
        if (newLayout == _lastLayout) return;

        _lastLayout = newLayout;
        if (_videoTable.Table is not DataTableSource dts) return;
        var dt = dts.DataTable;

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
        _videoTable.SetNeedsDraw();
    }

    private void ApplyColumnStyles(DataTable dt)
    {
        _videoTable.Style.ColumnStyles.Clear();
        _videoTable.Style.ColumnStyles[0] = new ColumnStyle
            { MinWidth = _lastLayout.NumberWidth, MaxWidth = _lastLayout.NumberWidth };
        _videoTable.Style.ColumnStyles[1] = new ColumnStyle
            { MinWidth = _lastLayout.TitleWidth };
        _videoTable.Style.ColumnStyles[2] = new ColumnStyle
            { MinWidth = _lastLayout.ChannelWidth, MaxWidth = _lastLayout.ChannelWidth };
        _videoTable.Style.ColumnStyles[3] = new ColumnStyle
            { MinWidth = _lastLayout.AddedWidth, MaxWidth = _lastLayout.AddedWidth };
        _videoTable.Style.ColumnStyles[4] = new ColumnStyle
        {
            MinWidth = _lastLayout.StatusWidth, MaxWidth = _lastLayout.StatusWidth,
            ColorGetter = args =>
            {
                var val = args.CellValue?.ToString() ?? "";
                if (val.StartsWith(Glyphs.StatusRemoved, StringComparison.Ordinal)) return SchemeManager.GetScheme(Theme.SchemeStatusRemoved);
                if (string.Equals(val, Glyphs.StatusActive, StringComparison.Ordinal)) return SchemeManager.GetScheme(Theme.SchemeStatusActive);
                return null;
            },
        };
    }

    private async void OnProfileSelected(object? sender, ValueChangedEventArgs<int?> e)
    {
        if (_suppressEvents) return;
        try
        {
            var idx = e.NewValue ?? -1;
            if (idx >= 0 && idx < _profiles.Count)
            {
                var profile = _profiles[idx];
                if (profile == _selectedProfile) return;
                _selectedProfile = profile;
                _selectedPlaylist = null;
                _videoTable.Table = null;
                _videoStatusLabel.Text = " Videos";
                await SwitchProfileApiService().ConfigureAwait(false);
                await RefreshPlaylistsAsync().ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load profile");
        }
    }

    private void OnPlaylistSelected(object? sender, ValueChangedEventArgs<int?> e)
    {
        if (_suppressEvents) return;
        var idx = e.NewValue ?? -1;
        if (idx < 0 || idx >= _playlists.Count) return;

        _selectedPlaylist = _playlists[idx];
        LoadVideosForSelectedPlaylist();
    }

    private void LoadVideosForSelectedPlaylist()
    {
        var playlist = _selectedPlaylist;
        if (playlist is null) return;

        // Load videos in background to avoid UI stutter on large playlists
        _ = Task.Run(async () =>
        {
            try
            {
                await using var bgScope = scopeFactory.CreateAsyncScope();
                var bgRepo = bgScope.ServiceProvider.GetRequiredService<IPlaylistRepository>();
                var videos = (_showDeletedOnly
                    ? await bgRepo.GetDeletedVideosAsync(playlist.Id)
.ConfigureAwait(false) : await bgRepo.GetVideosAsync(playlist.Id).ConfigureAwait(false)).ToList();

                TGuiApp.Invoke(() =>
                {
                    if (_selectedPlaylist?.Id != playlist.Id) return;
                    _videos = videos;
                    ApplyFilterAndSort();
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load playlist videos");
            }
        });
    }
}
