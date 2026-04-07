using System.Data;
using Terminal.Gui;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Domain.Models;
using YTPlaylistTracker.Infrastructure.Platform;
using YTPlaylistTracker.Infrastructure.YouTube;
using Microsoft.Extensions.Logging;

namespace YTPlaylistTracker.UI.Views;

public partial class MainWindow(
    IPlaylistRepository playlistRepo,
    IProfileRepository profileRepo,
    ISyncService syncService,
    IYouTubeApiService youtubeApi,
    IBrowserLauncher browser,
    IUserSettings userSettings,
    IUpdateService updateService,
    ILogger<MainWindow> logger) : Window("ytpt - YouTube Playlist Tracker")
{
    private UpdateInfo? _latestUpdate;

    private ListView _profileList = null!;
    private ListView _playlistList = null!;
    private TableView _videoTable = null!;
    private FrameView _videoFrame = null!;

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
    private object? _searchDebounceTimer;
    private string _sortColumn = "Added Date";
    private bool _sortAscending;
    private bool _isSyncing;
    private ColumnWidths _lastLayout;

    private string DefaultTitle => _latestUpdate is { IsUpdateAvailable: true }
        ? $"ytpt - YouTube Playlist Tracker (v{_latestUpdate.LatestVersion} available!)"
        : "ytpt - YouTube Playlist Tracker";

    private void SetupUI()
    {
        var profileFrame = new FrameView("Profiles")
        {
            X = 0, Y = 0,
            Width = 18,
            Height = Dim.Fill(2)
        };
        _profileList = new ListView
        {
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _profileList.SelectedItemChanged += OnProfileSelected;
        profileFrame.Add(_profileList);

        var playlistFrame = new FrameView("Playlists")
        {
            X = Pos.Right(profileFrame),
            Y = 0,
            Width = 28,
            Height = Dim.Fill(2)
        };
        _playlistList = new ListView
        {
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };
        _playlistList.SelectedItemChanged += OnPlaylistSelected;
        playlistFrame.Add(_playlistList);

        _videoFrame = new FrameView("Videos")
        {
            X = Pos.Right(playlistFrame),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(2)
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
                ExpandLastColumn = false,
                AlwaysShowHeaders = true,
                ColumnStyles = new Dictionary<DataColumn, TableView.ColumnStyle>()
            }
        };
        _videoTable.CellActivated += (args) => ShowDetail();
        _videoFrame.Add(_videoTable);
        _videoTable.LayoutComplete += (_) => OnVideoTableResized();

        _profileList.OpenSelectedItem += (args) => ShowDetail();
        _playlistList.OpenSelectedItem += (args) => ShowDetail();

        Add(profileFrame, playlistFrame, _videoFrame);

        var hintBar1 = new Label(" h/l:pane  j/k:nav  J/K:fast  Enter:detail  /:search  o:sort  a:add  t:track  T:all  s:sync  S:all  q:quit")
        {
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill(),
            ColorScheme = Colors.Menu
        };
        var hintBar2 = new Label(" F1:add  F2:track  F5:sync  F6:sync all  F8:deleted  F9:settings  e:export  H:history  u:update  ?:help")
        {
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            ColorScheme = Colors.Menu
        };
        Add(hintBar1, hintBar2);
    }

    public async Task InitializeAsync()
    {
        SetupUI();
        _profiles = (await profileRepo.GetAllAsync()).ToList();
        if (_profiles.Count == 0)
        {
            var defaultProfile = new Profile { Name = "Default", IsDefault = true };
            await profileRepo.AddAsync(defaultProfile);
            _profiles = (await profileRepo.GetAllAsync()).ToList();
        }

        _selectedProfile = _profiles.FirstOrDefault(p => p.IsDefault) ?? _profiles.First();
        RefreshProfileList();
        await RefreshPlaylistsAsync();

        // Load videos for the initially selected playlist
        if (_selectedPlaylist is not null)
            await RefreshVideosAsync();

        // Check if user is logged in before starting background work
        if (!YouTubeApiService.HasStoredToken("default") &&
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("YOUTUBE_API_KEY")))
        {
            global::Terminal.Gui.Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(100), _ =>
            {
                MessageBox.Query("Welcome to ytpt",
                    "You're not logged in yet.\n\n" +
                    "To get started, close the app and run:\n" +
                    "  ytpt login\n\n" +
                    "This will open your browser to sign in with Google\n" +
                    "and grant YouTube read-only access.",
                    "OK");
                return false;
            });
            return;
        }

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
                    logger.LogError(ex, "Background playlist fetch failed");
                }

                // Backfill channel info if not yet populated
                if (capturedProfile is not null && capturedProfile.ChannelTitle is null)
                {
                    try
                    {
                        var channel = await youtubeApi.GetMyChannelAsync();
                        if (channel is not null)
                        {
                            capturedProfile.YouTubeChannelId = channel.ChannelId;
                            capturedProfile.ChannelTitle = channel.Title;
                            capturedProfile.ChannelThumbnailUrl = channel.ThumbnailUrl;
                            await profileRepo.UpdateAsync(capturedProfile);
                            global::Terminal.Gui.Application.MainLoop.Invoke(() => RefreshProfileList());
                        }
                    }
                    catch (Exception ex) { logger.LogWarning(ex, "Failed to fetch channel info"); }
                }

                // Check for updates in background
                if (userSettings.CheckForUpdatesOnStartup)
                {
                    try
                    {
                        var updateResult = await updateService.CheckForUpdateAsync();
                        global::Terminal.Gui.Application.MainLoop.Invoke(() =>
                        {
                            _latestUpdate = updateResult;
                            if (updateResult.IsUpdateAvailable)
                            {
                                Title = $"ytpt - YouTube Playlist Tracker (v{updateResult.LatestVersion} available!)";
                                SetNeedsDisplay();
                            }
                        });
                    }
                    catch (Exception ex) { logger.LogWarning(ex, "Update check failed"); }
                }

                if (userSettings.AutoSyncOnStartup && capturedProfile is not null)
                {
                    _isSyncing = true;
                    global::Terminal.Gui.Application.MainLoop.Invoke(() =>
                        ShowSpinner("Auto-syncing all playlists..."));
                    try
                    {
                        var syncProgress = new Progress<string>(msg =>
                            global::Terminal.Gui.Application.MainLoop.Invoke(() => ShowSpinner(msg)));
                        var results = await syncService.SyncAllTrackedAsync(capturedProfile.Id, syncProgress);
                        int totalAdded = results.Values.Sum(r => r.Added);
                        int totalRemoved = results.Values.Sum(r => r.Removed);
                        logger.LogInformation("Auto-sync complete: {Count} playlists, +{Added} -{Removed}",
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
                                Title = DefaultTitle;
                                SetNeedsDisplay();
                                return false;
                            });
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Auto-sync failed");
                        global::Terminal.Gui.Application.MainLoop.Invoke(() => HideSpinner());
                    }
                    finally
                    {
                        _isSyncing = false;
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
        var names = _profiles.Select(p => p.IsDefault ? "> " + (p.ChannelTitle ?? p.Name) : "  " + (p.ChannelTitle ?? p.Name)).ToList();
        _profileList.SetSource(names);
        var idx = _profiles.IndexOf(_selectedProfile!);
        if (idx >= 0) _profileList.SelectedItem = idx;
    }

    private static bool IsLikedPlaylist(Playlist p) =>
        p.YouTubePlaylistId.StartsWith("LL", StringComparison.Ordinal);

    private async Task RefreshPlaylistsAsync()
    {
        if (_selectedProfile is null) return;
        var prevIdx = _playlistList.SelectedItem;
        _playlists = (await playlistRepo.GetByProfileAsync(_selectedProfile.Id))
            .OrderByDescending(p => IsLikedPlaylist(p))
            .ThenBy(p => p.Title ?? p.YouTubePlaylistId)
            .ToList();
        var names = _playlists.Select(p =>
        {
            var prefix = p.IsTracked ? "[x] " : "[ ] ";
            return IsLikedPlaylist(p)
                ? prefix + "♥ " + (p.Title ?? p.YouTubePlaylistId)
                : prefix + (p.Title ?? p.YouTubePlaylistId);
        }).ToList();
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
        Title = DefaultTitle;
        SetNeedsDisplay();
    }
}
