using System.Data;
using System.Net.Http;
using Google;
using Terminal.Gui;
using YTPlaylistTracker.Application.Helpers;
using YTPlaylistTracker.Application.Services;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Infrastructure.Platform;
using Microsoft.Extensions.Logging;

namespace YTPlaylistTracker.UI.Views;

public class MainWindow : Window
{
    private readonly IPlaylistRepository _playlistRepo;
    private readonly IProfileRepository _profileRepo;
    private readonly ISyncService _syncService;
    private readonly IYouTubeApiService _youtubeApi;
    private readonly IBrowserLauncher _browser;
    private readonly IUserSettings _userSettings;
    private readonly ILogger _logger;

    private readonly ListView _profileList;
    private readonly ListView _playlistList;
    private readonly TableView _videoTable;
    private readonly FrameView _videoFrame;

    private List<Profile> _profiles = [];
    private List<Playlist> _playlists = [];
    private List<Video> _videos = [];
    private List<Video> _filteredVideos = [];
    private Profile? _selectedProfile;
    private Playlist? _selectedPlaylist;
    private bool _showDeletedOnly;
    private DateTime _lastCtrlC = DateTime.MinValue;
    private object? _spinnerTimer;
    private int _spinnerFrame;
    private static readonly string[] SpinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
    private bool _suppressEvents;
    private TextField? _searchField;
    private string _searchQuery = "";
    private string _sortColumn = "";
    private bool _sortAscending = true;

    public MainWindow(
        IPlaylistRepository playlistRepo,
        IProfileRepository profileRepo,
        ISyncService syncService,
        IYouTubeApiService youtubeApi,
        IBrowserLauncher browser,
        IUserSettings userSettings,
        ILogger<MainWindow> logger) : base("ytpt - YouTube Playlist Tracker")
    {
        _playlistRepo = playlistRepo;
        _profileRepo = profileRepo;
        _syncService = syncService;
        _youtubeApi = youtubeApi;
        _browser = browser;
        _userSettings = userSettings;
        _logger = logger;

        // Left pane: Profiles
        var profileFrame = new FrameView("Profiles")
        {
            X = 0, Y = 0,
            Width = 18,
            Height = Dim.Fill(1)
        };
        _profileList = new ListView
        {
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _profileList.SelectedItemChanged += OnProfileSelected;
        profileFrame.Add(_profileList);

        // Middle pane: Playlists
        var playlistFrame = new FrameView("Playlists")
        {
            X = Pos.Right(profileFrame),
            Y = 0,
            Width = 28,
            Height = Dim.Fill(1)
        };
        _playlistList = new ListView
        {
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _playlistList.SelectedItemChanged += OnPlaylistSelected;
        playlistFrame.Add(_playlistList);

        // Right pane: Videos (TableView with minimal borders)
        _videoFrame = new FrameView("Videos")
        {
            X = Pos.Right(playlistFrame),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1)
        };
        _videoTable = new TableView
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            FullRowSelect = true,
            Style = new TableView.TableStyle
            {
                ShowVerticalCellLines = false,
                ShowVerticalHeaderLines = false,
                ShowHorizontalHeaderOverline = false,
                ShowHorizontalHeaderUnderline = true,
                ExpandLastColumn = true,
                AlwaysShowHeaders = true,
                ColumnStyles = new Dictionary<DataColumn, TableView.ColumnStyle>()
            }
        };
        _videoTable.CellActivated += (args) => ShowDetail();
        _videoFrame.Add(_videoTable);

        _profileList.OpenSelectedItem += (args) => ShowDetail();
        _playlistList.OpenSelectedItem += (args) => ShowDetail();

        Add(profileFrame, playlistFrame, _videoFrame);

        var hintBar = new Label(" h/l:pane  j/k:nav  Enter:detail  /:search  o:sort  e:export  H:history  ?:help  a:add  t:track  s:sync  S:all  q:quit")
        {
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            ColorScheme = Colors.Menu
        };
        Add(hintBar);
    }

    public override bool ProcessHotKey(KeyEvent keyEvent)
    {
        // Don't intercept keys when search field is active
        if (_searchField is not null && _searchField.HasFocus)
            return base.ProcessHotKey(keyEvent);

        // Pane switching (arrows + h/l)
        var panes = new View[] { _profileList, _playlistList, _videoTable };
        var current = Array.FindIndex(panes, p => p.HasFocus);

        switch (keyEvent.Key)
        {
            case Key.CursorLeft or Key.h when current > 0:
                panes[current - 1].SetFocus();
                return true;
            case Key.CursorRight or Key.l when current >= 0 && current < panes.Length - 1:
                panes[current + 1].SetFocus();
                return true;
        }

        // Focused pane for j/k navigation
        var focused = _profileList.HasFocus ? (View)_profileList
            : _playlistList.HasFocus ? (View)_playlistList
            : (View)_videoTable;

        // All single-letter keybinds in ProcessHotKey so child views don't eat them
        switch (keyEvent.KeyValue)
        {
            case 'j': focused.ProcessKey(new KeyEvent(Key.CursorDown, new KeyModifiers())); return true;
            case 'k': focused.ProcessKey(new KeyEvent(Key.CursorUp, new KeyModifiers())); return true;
            case 'a': _ = OnAddByUrlAsync(); return true;
            case 't': OnToggleTrack(); return true;
            case 'T': OnToggleAllTracking(); return true;
            case 's': OnSync(); return true;
            case 'S': OnSyncAll(); return true;
            case 'e': _ = OnExport(); return true;
            case 'H': _ = OnShowHistory(); return true;
            case 'o': ShowSortMenu(); return true;
            case 'q': global::Terminal.Gui.Application.RequestStop(); return true;
            case '/': ShowSearch(); return true;
            case '?':
                global::Terminal.Gui.Application.Run(new HelpDialog());
                return true;
        }

        return base.ProcessHotKey(keyEvent);
    }

    public override bool ProcessKey(KeyEvent keyEvent)
    {
        // Tab / Shift+Tab: cycle focus between the three panes
        if (keyEvent.Key == Key.Tab || keyEvent.Key == Key.BackTab)
        {
            var panes = new View[] { _profileList, _playlistList, _videoTable };
            var current = Array.FindIndex(panes, p => p.HasFocus);
            if (current < 0) current = 0;
            int next = keyEvent.Key == Key.Tab
                ? (current + 1) % panes.Length
                : (current - 1 + panes.Length) % panes.Length;
            panes[next].SetFocus();
            return true;
        }

        // F-keys in ProcessKey (child views don't consume them)
        switch (keyEvent.Key)
        {
            case Key.F1: _ = OnAddByUrlAsync(); return true;
            case Key.F2: OnToggleTrack(); return true;
            case Key.F5: OnSync(); return true;
            case Key.F6: OnSyncAll(); return true;
            case Key.F8: OnToggleDeleted(); return true;
            case Key.F9: OnSettings(); return true;
            case Key.F10: global::Terminal.Gui.Application.RequestStop(); return true;
        }

        if (keyEvent.Key == (Key.C | Key.CtrlMask))
        {
            var now = DateTime.UtcNow;
            if ((now - _lastCtrlC).TotalMilliseconds < 1000)
            {
                global::Terminal.Gui.Application.RequestStop();
                return true;
            }
            _lastCtrlC = now;
            Title = "ytpt - Press Ctrl+C again to quit";
            SetNeedsDisplay();
            global::Terminal.Gui.Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(2), _ =>
            {
                Title = "ytpt - YouTube Playlist Tracker";
                SetNeedsDisplay();
                return false;
            });
            return true;
        }
        return base.ProcessKey(keyEvent);
    }

    public async Task InitializeAsync()
    {
        _profiles = await _profileRepo.GetAllAsync();
        if (_profiles.Count == 0)
        {
            var defaultProfile = new Profile { Name = "Default", IsDefault = true };
            await _profileRepo.AddAsync(defaultProfile);
            _profiles = await _profileRepo.GetAllAsync();
        }

        _selectedProfile = _profiles.FirstOrDefault(p => p.IsDefault) ?? _profiles.First();
        RefreshProfileList();
        await RefreshPlaylistsAsync();

        // Load videos for the initially selected playlist
        if (_selectedPlaylist is not null)
            await RefreshVideosAsync();

        // Fetch playlists from YouTube in background after UI renders, then auto-sync if enabled
        var capturedProfile = _selectedProfile;
        global::Terminal.Gui.Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(100), _ =>
        {
            ShowSpinner("Fetching playlists...");
            Task.Run(async () =>
            {
                try
                {
                    await FetchAndImportAllPlaylists(capturedProfile);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background playlist fetch failed");
                }

                if (_userSettings.AutoSyncOnStartup && capturedProfile is not null)
                {
                    global::Terminal.Gui.Application.MainLoop.Invoke(() =>
                        ShowSpinner("Auto-syncing all playlists..."));
                    try
                    {
                        var results = await _syncService.SyncAllTrackedAsync(capturedProfile.Id);
                        int totalAdded = results.Values.Sum(r => r.Added);
                        int totalRemoved = results.Values.Sum(r => r.Removed);
                        _logger.LogInformation("Auto-sync complete: {Count} playlists, +{Added} -{Removed}",
                            results.Count, totalAdded, totalRemoved);
                        global::Terminal.Gui.Application.MainLoop.Invoke(() =>
                        {
                            HideSpinner();
                            RefreshPlaylistsAsync().GetAwaiter().GetResult();
                            RefreshVideosAsync().GetAwaiter().GetResult();
                            Title = $"ytpt - Synced {results.Count} playlists (+{totalAdded} -{totalRemoved})";
                            SetNeedsDisplay();
                            global::Terminal.Gui.Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(5), _ =>
                            {
                                Title = "ytpt - YouTube Playlist Tracker";
                                SetNeedsDisplay();
                                return false;
                            });
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Auto-sync failed");
                        global::Terminal.Gui.Application.MainLoop.Invoke(() => HideSpinner());
                    }
                }
                else
                {
                    global::Terminal.Gui.Application.MainLoop.Invoke(() =>
                    {
                        HideSpinner();
                        RefreshPlaylistsAsync().GetAwaiter().GetResult();
                    });
                }
            });
            return false;
        });
    }

    private void RefreshProfileList()
    {
        var names = _profiles.Select(p => p.IsDefault ? "> " + p.Name : "  " + p.Name).ToList();
        _profileList.SetSource(names);
        var idx = _profiles.IndexOf(_selectedProfile!);
        if (idx >= 0) _profileList.SelectedItem = idx;
    }

    private async Task RefreshPlaylistsAsync()
    {
        if (_selectedProfile is null) return;
        var prevIdx = _playlistList.SelectedItem;
        _playlists = await _playlistRepo.GetByProfileAsync(_selectedProfile.Id);
        var names = _playlists.Select(p =>
            (p.IsTracked ? "[x] " : "[ ] ") + (p.Title ?? p.YouTubePlaylistId)).ToList();
        _suppressEvents = true;
        _playlistList.SetSource(names);
        if (prevIdx >= 0 && prevIdx < _playlists.Count)
        {
            _playlistList.SelectedItem = prevIdx;
            _selectedPlaylist = _playlists[prevIdx];
        }
        else if (_playlists.Count > 0)
        {
            _playlistList.SelectedItem = 0;
            _selectedPlaylist = _playlists[0];
        }
        _suppressEvents = false;
    }

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

        _videos = _showDeletedOnly
            ? await _playlistRepo.GetDeletedVideosAsync(_selectedPlaylist.Id)
            : await _playlistRepo.GetVideosAsync(_selectedPlaylist.Id);

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
            for (int i = 0; i < filtered.Count; i++)
            {
                var v = filtered[i];
                var t = (v.Title ?? "").Trim();
                dt.Rows.Add(
                    (i + 1).ToString(),
                    t,
                    v.ChannelTitle ?? "",
                    (v.AddedAt?.ToString("yyyy-MM-dd") ?? "") + "  ",
                    v.DeletedAt.HasValue
                        ? "X " + (v.RemovalReason?.ToString() ?? "Removed")
                        : "Active");
            }
        }

        _filteredVideos = filtered;
        _videoTable.Table = dt;

        _videoTable.Style.ColumnStyles.Clear();
        _videoTable.Style.ColumnStyles[dt.Columns[0]] = new TableView.ColumnStyle { MinWidth = 3, MaxWidth = 5 };
        _videoTable.Style.ColumnStyles[dt.Columns[2]] = new TableView.ColumnStyle { MinWidth = 8, MaxWidth = 25 };
        _videoTable.Style.ColumnStyles[dt.Columns[3]] = new TableView.ColumnStyle { MinWidth = 10, MaxWidth = 12 };
        _videoTable.Style.ColumnStyles[dt.Columns[4]] = new TableView.ColumnStyle { MinWidth = 6, MaxWidth = 16 };

        var sortIndicator = string.IsNullOrEmpty(_sortColumn) ? ""
            : " " + (_sortAscending ? "^" : "v") + _sortColumn;
        _videoFrame.Title = _showDeletedOnly
            ? "Removed (" + title + ") [" + filtered.Count + "]" + sortIndicator
            : "Videos (" + title + ") [" + filtered.Count + "]" + sortIndicator;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh videos");
        }
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
            _logger.LogError(ex, "Failed to load profile");
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
            _logger.LogError(ex, "Failed to load playlist");
        }
    }

    private void ShowSpinner(string message)
    {
        HideSpinner();
        _spinnerFrame = 0;
        _spinnerTimer = global::Terminal.Gui.Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(200), _ =>
        {
            Title = "ytpt " + SpinnerFrames[_spinnerFrame % SpinnerFrames.Length] + " " + message;
            _spinnerFrame++;
            return true;
        });
    }

    private void HideSpinner()
    {
        if (_spinnerTimer is not null)
        {
            global::Terminal.Gui.Application.MainLoop.RemoveTimeout(_spinnerTimer);
            _spinnerTimer = null;
        }
        Title = "ytpt - YouTube Playlist Tracker";
        SetNeedsDisplay();
    }

    private async Task FetchAndImportAllPlaylists(Profile? profile)
    {
        if (profile is null) return;

        try
        {
            var userPlaylists = await _youtubeApi.GetUserPlaylistsAsync();
            if (userPlaylists.Count == 0) return;

            var dbPlaylists = await _playlistRepo.GetByProfileAsync(profile.Id);
            var existingIds = dbPlaylists.Select(p => p.YouTubePlaylistId).ToHashSet();

            int added = 0;
            foreach (var meta in userPlaylists)
            {
                if (existingIds.Contains(meta.PlaylistId)) continue;

                var playlist = new Playlist
                {
                    ProfileId = profile.Id,
                    YouTubePlaylistId = meta.PlaylistId,
                    Title = meta.Title,
                    IsTracked = false,
                    Description = meta.Description,
                    ThumbnailUrl = meta.ThumbnailUrl,
                    PublishedAt = meta.PublishedAt,
                    JsonMetadata = meta.JsonMetadata
                };
                await _playlistRepo.AddAsync(playlist);
                added++;
            }

            if (added > 0)
            {
                _logger.LogInformation("Imported {Count} new playlists from YouTube", added);
                global::Terminal.Gui.Application.MainLoop.Invoke(async () => await RefreshPlaylistsAsync());
            }
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            _logger.LogWarning("YouTube API quota/auth issue during background fetch — skipping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch playlists from YouTube");
        }
    }

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
        _logger.LogInformation("Adding playlist: {PlaylistId}", playlistId);

        var playlist = new Playlist
        {
            ProfileId = _selectedProfile.Id,
            YouTubePlaylistId = playlistId,
            IsTracked = true
        };

        try
        {
            var meta = await _youtubeApi.GetPlaylistMetadataAsync(playlistId);
            if (meta is not null)
            {
                playlist.Title = meta.Title;
                playlist.Description = meta.Description;
                playlist.ThumbnailUrl = meta.ThumbnailUrl;
                playlist.PublishedAt = meta.PublishedAt;
                playlist.JsonMetadata = meta.JsonMetadata;
            }

            await _playlistRepo.AddAsync(playlist);
            await RefreshPlaylistsAsync();
            MessageBox.Query("Success", "Added playlist: " + (playlist.Title ?? playlistId), "OK");
        }
        catch (GoogleApiException ex)
        {
            _logger.LogError(ex, "YouTube API error adding playlist");
            MessageBox.Query("YouTube Error", "Could not fetch playlist: " + ex.Message, "OK");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error adding playlist");
            MessageBox.Query("Network Error", "Could not connect to YouTube. Check your internet connection.", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add playlist");
            MessageBox.Query("Error", "Failed to add playlist: " + ex.Message, "OK");
        }
    }

    private async void OnToggleTrack()
    {
        if (_selectedPlaylist is null) return;
        try
        {
            _selectedPlaylist.IsTracked = !_selectedPlaylist.IsTracked;
            await _playlistRepo.UpdateAsync(_selectedPlaylist);
            await RefreshPlaylistsAsync();
        }
        catch (Exception ex)
        {
            _selectedPlaylist.IsTracked = !_selectedPlaylist.IsTracked; // revert
            _logger.LogError(ex, "Failed to toggle tracking");
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
                await _playlistRepo.UpdateAsync(p);
            }
            await RefreshPlaylistsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle all tracking");
            MessageBox.Query("Error", "Failed to update: " + ex.Message, "OK");
        }
    }

    private async void OnSync()
    {
        if (_selectedPlaylist is null)
        {
            MessageBox.Query("Info", "Select a playlist first.", "OK");
            return;
        }

        ShowSpinner("Syncing " + _selectedPlaylist.Title + "...");
        try
        {
            var result = await Task.Run(() => _syncService.SyncPlaylistAsync(_selectedPlaylist));
            HideSpinner();
            await RefreshVideosAsync();
            MessageBox.Query("Sync Complete",
                "+" + result.Added + " added, -" + result.Removed + " removed, ~" + result.Updated + " updated", "OK");
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            HideSpinner();
            _logger.LogError(ex, "YouTube API quota or auth error during sync");
            MessageBox.Query("YouTube Error", "API quota exceeded or auth expired.\nTry: ytpt login", "OK");
        }
        catch (GoogleApiException ex)
        {
            HideSpinner();
            _logger.LogError(ex, "YouTube API error during sync");
            MessageBox.Query("YouTube Error", "Sync failed: " + ex.Message, "OK");
        }
        catch (HttpRequestException ex)
        {
            HideSpinner();
            _logger.LogError(ex, "Network error during sync");
            MessageBox.Query("Network Error", "Could not connect to YouTube. Check your internet connection.", "OK");
        }
        catch (Exception ex)
        {
            HideSpinner();
            _logger.LogError(ex, "Sync failed");
            MessageBox.Query("Error", "Sync failed: " + ex.Message, "OK");
        }
    }

    private async void OnSyncAll()
    {
        if (_selectedProfile is null) return;

        ShowSpinner("Syncing all playlists...");
        try
        {
            var results = await Task.Run(() => _syncService.SyncAllTrackedAsync(_selectedProfile.Id));
            HideSpinner();
            await RefreshPlaylistsAsync();
            await RefreshVideosAsync();
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
            _logger.LogError(ex, "YouTube API quota or auth error during sync all");
            MessageBox.Query("YouTube Error", "API quota exceeded or auth expired.\nTry: ytpt login", "OK");
        }
        catch (GoogleApiException ex)
        {
            HideSpinner();
            _logger.LogError(ex, "YouTube API error during sync all");
            MessageBox.Query("YouTube Error", "Sync failed: " + ex.Message, "OK");
        }
        catch (HttpRequestException ex)
        {
            HideSpinner();
            _logger.LogError(ex, "Network error during sync all");
            MessageBox.Query("Network Error", "Could not connect to YouTube. Check your internet connection.", "OK");
        }
        catch (Exception ex)
        {
            HideSpinner();
            _logger.LogError(ex, "Sync all failed");
            MessageBox.Query("Error", "Sync failed: " + ex.Message, "OK");
        }
    }

    private async void OnToggleDeleted()
    {
        _showDeletedOnly = !_showDeletedOnly;
        try { await RefreshVideosAsync(); }
        catch (Exception ex) { _logger.LogError(ex, "Failed to toggle deleted view"); }
    }

    private async void ShowDetail()
    {
        try
        {
            if (_profileList.HasFocus && _selectedProfile is not null)
            {
                var playlists = await _playlistRepo.GetByProfileAsync(_selectedProfile.Id);
                var tracked = playlists.Count(p => p.IsTracked);
                var dialog = DetailDialog.ForProfile(_selectedProfile, playlists.Count, tracked, _browser);
                global::Terminal.Gui.Application.Run(dialog);
            }
            else if (_playlistList.HasFocus && _selectedPlaylist is not null)
            {
                var videos = await _playlistRepo.GetVideosAsync(_selectedPlaylist.Id);
                var active = videos.Count(v => v.DeletedAt == null);
                var removed = videos.Count(v => v.DeletedAt != null);
                var dialog = DetailDialog.ForPlaylist(_selectedPlaylist, active, removed, _browser);
                global::Terminal.Gui.Application.Run(dialog);
            }
            else if (_videoTable.HasFocus && _videoTable.SelectedRow >= 0 && _videoTable.SelectedRow < _filteredVideos.Count)
            {
                var video = _filteredVideos[_videoTable.SelectedRow];
                var dialog = DetailDialog.ForVideo(video, _browser);
                global::Terminal.Gui.Application.Run(dialog);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show details");
        }
    }

    private void ShowSortMenu()
    {
        var dialog = new Dialog("Sort by", 30, 10);
        var options = new[] { "Title", "Channel", "Added Date", "Status" };
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

    private async Task OnShowHistory()
    {
        if (_selectedProfile is null) return;
        try
        {
            var removedVideos = await _playlistRepo.GetAllDeletedVideosAsync(_selectedProfile.Id);
            var dialog = new RemovalHistoryDialog(removedVideos);
            global::Terminal.Gui.Application.Run(dialog);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show removal history");
            MessageBox.Query("Error", "Failed to load history: " + ex.Message, "OK");
        }
    }

    private async Task OnExport()
    {
        if (_selectedProfile is null) return;

        try
        {
            var removedVideos = await _playlistRepo.GetAllDeletedVideosAsync(_selectedProfile.Id);
            if (removedVideos.Count == 0)
            {
                MessageBox.Query("Export", "No removed videos to export.", "OK");
                return;
            }

            var dialog = new Dialog("Export Removed Videos", 50, 10);
            var formatLabel = new Label("Format:") { X = 1, Y = 1 };
            var formatRadio = new RadioGroup(new NStack.ustring[] { "CSV", "JSON" }) { X = 12, Y = 1 };
            var pathLabel = new Label("File:") { X = 1, Y = 3 };
            var defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "ytpt-removed-videos");
            var pathField = new TextField(defaultPath) { X = 12, Y = 3, Width = Dim.Fill(2) };
            var okBtn = new Button("Export", true);
            var cancelBtn = new Button("Cancel");

            string? resultPath = null;
            int selectedFormat = 0;
            okBtn.Clicked += () => { resultPath = pathField.Text?.ToString(); selectedFormat = formatRadio.SelectedItem; global::Terminal.Gui.Application.RequestStop(); };
            cancelBtn.Clicked += () => global::Terminal.Gui.Application.RequestStop();

            dialog.Add(formatLabel, formatRadio, pathLabel, pathField);
            dialog.AddButton(okBtn);
            dialog.AddButton(cancelBtn);
            global::Terminal.Gui.Application.Run(dialog);

            if (resultPath is null) return;

            var ext = selectedFormat == 1 ? ".json" : ".csv";
            if (!resultPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                resultPath += ext;

            var entries = ExportService.BuildEntries(removedVideos);
            var content = selectedFormat == 1
                ? ExportService.ToJson(entries)
                : ExportService.ToCsv(entries);

            await File.WriteAllTextAsync(resultPath, content);
            MessageBox.Query("Export Complete", $"Exported {entries.Count} removed videos to:\n{resultPath}", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed");
            MessageBox.Query("Error", "Export failed: " + ex.Message, "OK");
        }
    }

    private void OnSettings()
    {
        var settingsDialog = new SettingsDialog(_playlistRepo, _selectedPlaylist, _userSettings);
        global::Terminal.Gui.Application.Run(settingsDialog);
    }
}
