using System.Data;
using Microsoft.Extensions.DependencyInjection;
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
    IYouTubeApiServiceFactory youtubeApiFactory,
    ISystemLauncher browser,
    IUserSettings userSettings,
    IUpdateService updateService,
    IServiceScopeFactory scopeFactory,
    ILogger<MainWindow> logger) : Window()
{
    private IYouTubeApiService? _youtubeApi;
    private UpdateInfo? _latestUpdate;
    private bool _updateInstalled;

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
    private SpinnerView _spinner = null!;
    private Label _spinnerMessage = null!;
    private bool _suppressEvents;

    private IDisposable SuppressEvents()
    {
        _suppressEvents = true;
        return new EventGuard(this);
    }

    private readonly struct EventGuard(MainWindow owner) : IDisposable
    {
        public void Dispose() => owner._suppressEvents = false;
    }
    private TextField? _searchField;
    private string _searchQuery = "";
    private object? _searchDebounceTimer;
    private string _sortColumn = "Added Date";
    private bool _sortAscending;
    private bool _isSyncing;
    private ColumnWidths _lastLayout;

    private string DefaultTitle => _latestUpdate is { IsUpdateAvailable: true }
        ? $" ytpt - YouTube Playlist Tracker (v{_latestUpdate.LatestVersion} available!) "
        : " ytpt - YouTube Playlist Tracker ";

    protected override void Dispose(bool disposing)
    {
        if (disposing) CleanupCommands();
        base.Dispose(disposing);
    }

    public async Task InitializeAsync()
    {
        SetupUI();
        RegisterCommands();
        _profiles = (await profileRepo.GetAllAsync().ConfigureAwait(false)).ToList();

        // First-run: show welcome dialog
        if (_profiles.Count == 0)
        {
            global::Terminal.Gui.Application.AddTimeout(TimeSpan.FromMilliseconds(100), () =>
            {
                var welcome = new WelcomeDialog();
                global::Terminal.Gui.Application.Run(welcome);
                HandleWelcomeChoice(welcome.Choice);
                return false;
            });
            // Create a temporary default profile so the UI has something to show
            var tempProfile = new Profile { Name = "Default", IsDefault = true, IsOffline = true };
            await profileRepo.AddAsync(tempProfile).ConfigureAwait(false);
            _profiles = (await profileRepo.GetAllAsync().ConfigureAwait(false)).ToList();
        }

        _selectedProfile = _profiles.FirstOrDefault(p => p.IsDefault) ?? _profiles[0];
        RefreshProfileList();
        await RefreshPlaylistsAsync().ConfigureAwait(false);

        // Load videos for the initially selected playlist
        if (_selectedPlaylist is not null)
            await RefreshVideosAsync().ConfigureAwait(false);

        // Create YouTube API service for the selected profile
        try
        {
            _youtubeApi = await youtubeApiFactory.CreateForProfileAsync(_selectedProfile).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to create YouTube API service for profile {Profile}", _selectedProfile.Name);
        }

        // Skip background work if not authenticated and no API key available
        if (!youtubeApiFactory.IsAuthenticated(_selectedProfile) &&
            string.IsNullOrWhiteSpace(Infrastructure.Configuration.AppSettings.YouTubeApiKey))
            return;

        StartBackgroundWork();
    }

    private void StartBackgroundWork()
    {
        var capturedProfile = _selectedProfile;
        global::Terminal.Gui.Application.AddTimeout(TimeSpan.FromMilliseconds(100), () =>
        {
            ShowSpinner("Fetching playlists...");
            _ = Task.Run(async () =>
            {
                await using var bgScope = scopeFactory.CreateAsyncScope();
                var bgPlaylistRepo = bgScope.ServiceProvider.GetRequiredService<IPlaylistRepository>();
                var bgProfileRepo = bgScope.ServiceProvider.GetRequiredService<IProfileRepository>();
                var bgSyncService = bgScope.ServiceProvider.GetRequiredService<ISyncService>();

                try
                {
                    await FetchAndImportAllPlaylists(capturedProfile, bgPlaylistRepo).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Background playlist fetch failed");
                }

                // Backfill channel info if not yet populated
                if (capturedProfile is not null && capturedProfile.ChannelTitle is null && _youtubeApi is not null)
                {
                    try
                    {
                        var channel = await _youtubeApi.GetMyChannelAsync().ConfigureAwait(false);
                        if (channel is not null)
                        {
                            capturedProfile.YouTubeChannelId = channel.ChannelId;
                            capturedProfile.ChannelTitle = channel.Title;
                            capturedProfile.ChannelThumbnailUrl = channel.ThumbnailUrl;
                            await bgProfileRepo.UpdateAsync(capturedProfile).ConfigureAwait(false);
                            global::Terminal.Gui.Application.Invoke(() => RefreshProfileList());
                        }
                    }
                    catch (Exception ex) { logger.LogWarning(ex, "Failed to fetch channel info"); }
                }

                await CheckForUpdatesAsync().ConfigureAwait(false);

                if (userSettings.AutoSyncOnStartup && capturedProfile is not null && _youtubeApi is not null)
                    await AutoSyncAllAsync(capturedProfile, bgSyncService).ConfigureAwait(false);
                else
                    InvokeUI(() =>
                    {
                        HideSpinner();
                        RefreshPlaylistsAsync().GetAwaiter().GetResult();
                    });
            });
            return false;
        });
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var updateResult = await updateService.CheckForUpdateAsync().ConfigureAwait(false);
            if (updateResult.IsUpdateAvailable && userSettings.AutoInstallUpdates)
            {
                logger.LogInformation("[Update] Auto-installing v{Version}", updateResult.LatestVersion);
                try
                {
                    await updateService.ApplyUpdateAsync(updateResult).ConfigureAwait(false);
                    global::Terminal.Gui.Application.Invoke(() =>
                    {
                        _latestUpdate = updateResult;
                        _updateInstalled = true;
                        Title = $" ytpt — v{updateResult.LatestVersion} installed, restart to apply ";
                        ColorScheme = Theme.UpdateInstalled;
                        SetNeedsDraw();
                    });
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "[Update] Auto-install failed, showing notification only");
                    global::Terminal.Gui.Application.Invoke(() =>
                    {
                        _latestUpdate = updateResult;
                        Title = DefaultTitle;
                        ColorScheme = Theme.UpdateAvailable;
                        SetNeedsDraw();
                    });
                }
            }
            else
            {
                global::Terminal.Gui.Application.Invoke(() =>
                {
                    _latestUpdate = updateResult;
                    if (updateResult.IsUpdateAvailable)
                    {
                        Title = DefaultTitle;
                        ColorScheme = Theme.UpdateAvailable;
                        SetNeedsDraw();
                    }
                });
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "Update check failed"); }
    }

    private async Task AutoSyncAllAsync(Profile profile, ISyncService bgSyncService)
    {
        if (_youtubeApi is null) return;
        _isSyncing = true;
        global::Terminal.Gui.Application.Invoke(() => ShowSpinner("Auto-syncing all playlists..."));
        try
        {
            var syncProgress = new Progress<string>(msg =>
                global::Terminal.Gui.Application.Invoke(() => ShowSpinner(msg)));
            var results = await bgSyncService.SyncAllTrackedAsync(profile.Id, _youtubeApi, syncProgress).ConfigureAwait(false);
            int totalAdded = results.Values.Sum(r => r.Added);
            int totalRemoved = results.Values.Sum(r => r.Removed);
            logger.LogInformation("Auto-sync complete: {Count} playlists, +{Added} -{Removed}",
                results.Count, totalAdded, totalRemoved);
            InvokeUI(() =>
            {
                HideSpinner();
                RefreshPlaylistsAsync().GetAwaiter().GetResult();
                RefreshVideosAsync().GetAwaiter().GetResult();
                Title = $" ytpt - Synced {results.Count} playlists (+{totalAdded} -{totalRemoved}) ";
                SetNeedsDraw();
                global::Terminal.Gui.Application.AddTimeout(TimeSpan.FromSeconds(5), () =>
                {
                    Title = DefaultTitle;
                    SetNeedsDraw();
                    return false;
                });
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Auto-sync failed");
            global::Terminal.Gui.Application.Invoke(() => HideSpinner());
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void InvokeUI(Action action) =>
        global::Terminal.Gui.Application.Invoke(() =>
        {
            try { action(); }
            catch (Exception ex)
            {
                logger.LogError(ex, "UI callback failed");
                try { Dialogs.Query("Error", ex.Message, "OK"); } catch { /* last resort */ }
            }
        });

    private void RefreshProfileList()
    {
        var names = _profiles.Select(p =>
        {
            var name = p.ChannelTitle ?? p.Name;
            var prefix = p.IsDefault ? Glyphs.DefaultMarker : "  ";
            var suffix = p.IsOffline && !youtubeApiFactory.IsAuthenticated(p) ? Glyphs.OfflineSuffix : "";
            return prefix + name + suffix;
        }).ToList();
        using (SuppressEvents())
        {
            _profileList.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(names));
            var idx = _profiles.IndexOf(_selectedProfile!);
            if (idx >= 0) _profileList.SelectedItem = idx;
        }
    }

    private async Task RefreshPlaylistsAsync()
    {
        if (_selectedProfile is null) return;
        var prevIdx = _playlistList.SelectedItem;
        var sortTracked = userSettings.SortTrackedFirst;
        _playlists = (await playlistRepo.GetByProfileAsync(_selectedProfile.Id).ConfigureAwait(false))
            .OrderBy(p => PlaylistPolicy.For(p.Kind).SortOrder)
            .ThenByDescending(p => sortTracked && p.IsTracked)
            .ThenBy(p => p.Title ?? p.YouTubePlaylistId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var names = _playlists.Select(p =>
        {
            var policy = PlaylistPolicy.For(p.Kind);
            var prefix = !policy.AllowAutoSync ? Glyphs.ManualOnly : p.IsTracked ? Glyphs.Tracked : Glyphs.Untracked;
            var icon = Glyphs.PlaylistIcon(p.Kind) is { Length: > 0 } ic ? ic + " " : "";
            return prefix + icon + (p.Title ?? p.YouTubePlaylistId);
        }).ToList();
        if (names.Count == 0)
            names.Add("  (no playlists — press S to sync)");
        using (SuppressEvents())
        {
            _playlistList.SetSource(new System.Collections.ObjectModel.ObservableCollection<string>(names));
            if (_playlists.Count == 0) { _selectedPlaylist = null; }
            else if (prevIdx >= 0 && prevIdx < _playlists.Count)
            {
                _playlistList.SelectedItem = prevIdx;
                _selectedPlaylist = _playlists[prevIdx];
            }
            else
            {
                _playlistList.SelectedItem = 0;
                _selectedPlaylist = _playlists[0];
            }
        }
        LoadVideosForSelectedPlaylist();
    }
}
