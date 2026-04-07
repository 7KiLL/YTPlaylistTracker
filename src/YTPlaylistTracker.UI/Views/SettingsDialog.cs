using Terminal.Gui;
using YTPlaylistTracker.Application.Services;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Domain.Models;
using YTPlaylistTracker.Infrastructure.Configuration;
using YTPlaylistTracker.Infrastructure.Platform;
using YTPlaylistTracker.Infrastructure.Update;

namespace YTPlaylistTracker.UI.Views;

public class SettingsDialog : Dialog
{
    public bool UpdateRequested { get; private set; }
    public UpdateInfo? UpdateInfo { get; private set; }

    public SettingsDialog(IPlaylistRepository playlistRepo, Playlist? selectedPlaylist,
        IUserSettings userSettings, IUpdateService updateService, ISystemLauncher? launcher = null)
        : base("Settings", 70, 28)
    {
        int y = 0;

        // ── General ──
        Add(new Label("── General ─────────────────────────────────────────────────") { X = 1, Y = y, ColorScheme = Colors.Menu });
        y += 1;

        var autoSyncCheck = new CheckBox("Auto-sync on startup", userSettings.AutoSyncOnStartup) { X = 2, Y = y };
        autoSyncCheck.Toggled += (_) => { userSettings.AutoSyncOnStartup = autoSyncCheck.Checked; userSettings.Save(); };
        Add(autoSyncCheck);
        y += 1;

        var updatesCheck = new CheckBox("Check for updates on startup", userSettings.CheckForUpdatesOnStartup) { X = 2, Y = y };
        updatesCheck.Toggled += (_) => { userSettings.CheckForUpdatesOnStartup = updatesCheck.Checked; userSettings.Save(); };
        Add(updatesCheck);
        y += 1;

        var autoInstallCheck = new CheckBox("Auto-install updates on startup", userSettings.AutoInstallUpdates) { X = 2, Y = y };
        autoInstallCheck.Toggled += (_) => { userSettings.AutoInstallUpdates = autoInstallCheck.Checked; userSettings.Save(); };
        Add(autoInstallCheck);
        y += 1;

        var sortTrackedCheck = new CheckBox("Sort tracked playlists first", userSettings.SortTrackedFirst) { X = 2, Y = y };
        sortTrackedCheck.Toggled += (_) => { userSettings.SortTrackedFirst = sortTrackedCheck.Checked; userSettings.Save(); };
        Add(sortTrackedCheck);
        y += 2;

        // ── Sync ──
        Add(new Label("── Sync ────────────────────────────────────────────────────") { X = 1, Y = y, ColorScheme = Colors.Menu });
        y += 1;

        var likedPolicy = PlaylistPolicy.For(Domain.Enums.PlaylistKind.Liked);
        var cooldownText = likedPolicy.ManualCooldown is { } cd ? $"{cd.TotalHours:0}h" : "none";
        Add(new Label($"  Liked Videos cooldown:  {cooldownText}  (large playlist guard)") { X = 1, Y = y });
        y += 1;

        Add(new Label($"  Auto-sync cooldown:    {SyncService.AutoSyncCooldown.TotalHours:0}h   (between background syncs)") { X = 1, Y = y });
        y += 2;

        // ── Data ──
        Add(new Label("── Data ────────────────────────────────────────────────────") { X = 1, Y = y, ColorScheme = Colors.Menu });
        y += 1;

        Add(new Label("  Database:") { X = 1, Y = y });
        var dbBtn = new Button(AppSettings.DbPath) { X = 14, Y = y, ColorScheme = Colors.Menu };
        dbBtn.Clicked += () => launcher?.OpenPath(AppSettings.DbPath);
        Add(dbBtn);
        y += 1;
        Add(new Label("  Logs:") { X = 1, Y = y });
        var logBtn = new Button(AppSettings.LogDir) { X = 14, Y = y, ColorScheme = Colors.Menu };
        logBtn.Clicked += () => launcher?.OpenPath(AppSettings.LogDir);
        Add(logBtn);
        y += 1;

        var purgeBtn = new Button("Purge Deleted Videos") { X = 2, Y = y };
        purgeBtn.Clicked += async () =>
        {
            if (selectedPlaylist is null)
            {
                MessageBox.Query("Info", "Select a playlist first.", "OK");
                return;
            }
            var confirm = MessageBox.Query("Confirm Purge",
                $"Permanently delete all removed videos from\n\"{selectedPlaylist.Title ?? selectedPlaylist.YouTubePlaylistId}\"?\n\nThis cannot be undone.",
                "Purge", "Cancel");
            if (confirm == 0)
            {
                try
                {
                    await playlistRepo.PurgeDeletedVideosAsync(selectedPlaylist.Id);
                    MessageBox.Query("Done", "Deleted videos purged.", "OK");
                }
                catch (Exception ex)
                {
                    MessageBox.Query("Error", "Purge failed: " + ex.Message, "OK");
                }
            }
        };

        var resetBtn = new Button("Reset Database") { X = 26, Y = y };
        resetBtn.Clicked += () =>
        {
            var confirm = MessageBox.Query("Reset Database",
                "Delete the entire database and restart?\nAll playlists and tracking data will be lost.\n\n" + AppSettings.DbPath,
                "Reset", "Cancel");
            if (confirm == 0)
            {
                try
                {
                    File.Delete(AppSettings.DbPath);
                    MessageBox.Query("Done", "Database deleted. The app will now quit.\nRestart to create a fresh database.", "OK");
                    global::Terminal.Gui.Application.Top.RequestStop();
                    global::Terminal.Gui.Application.RequestStop();
                }
                catch (Exception ex)
                {
                    MessageBox.Query("Error", "Failed to delete database: " + ex.Message, "OK");
                }
            }
        };
        Add(purgeBtn, resetBtn);
        y += 2;

        // ── About ──
        Add(new Label("── About ───────────────────────────────────────────────────") { X = 1, Y = y, ColorScheme = Colors.Menu });
        y += 1;

        Add(new Label($"  Version:  {UpdateService.GetCurrentVersion()}") { X = 1, Y = y });
        y += 1;
        var updateNowBtn = new Button("Update Now") { X = 2, Y = y };
        updateNowBtn.Clicked += () =>
        {
            Task.Run(async () =>
            {
                try
                {
                    var update = await updateService.CheckForUpdateAsync();
                    global::Terminal.Gui.Application.MainLoop.Invoke(() =>
                    {
                        if (update.IsUpdateAvailable)
                        {
                            UpdateRequested = true;
                            UpdateInfo = update;
                            global::Terminal.Gui.Application.RequestStop();
                        }
                        else
                        {
                            MessageBox.Query("Up to Date", $"You're on the latest version ({update.CurrentVersion}).", "OK");
                        }
                    });
                }
                catch (Exception ex)
                {
                    global::Terminal.Gui.Application.MainLoop.Invoke(() =>
                        MessageBox.Query("Error", $"Update check failed: {ex.Message}", "OK"));
                }
            });
        };
        Add(updateNowBtn);

        var closeBtn = new Button("Close", true);
        closeBtn.Clicked += () => global::Terminal.Gui.Application.RequestStop();
        AddButton(closeBtn);
    }
}
