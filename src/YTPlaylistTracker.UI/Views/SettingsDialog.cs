using Terminal.Gui;
using YTPlaylistTracker.Application.Services;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Domain.Models;
using YTPlaylistTracker.Infrastructure.Configuration;
using YTPlaylistTracker.Infrastructure.Platform;
using YTPlaylistTracker.Infrastructure.Update;

namespace YTPlaylistTracker.UI.Views;

public sealed class SettingsDialog : Dialog
{
    public bool UpdateRequested { get; private set; }
    public UpdateInfo? UpdateInfo { get; private set; }

    public SettingsDialog(IPlaylistRepository playlistRepo, Playlist? selectedPlaylist,
        IUserSettings userSettings, IUpdateService updateService, ISystemLauncher? launcher = null)
        : base()
    {
        Title = "Settings";
        Width = 70;
        Height = 30;
        ShadowStyle = ShadowStyle.None;
        BorderStyle = LineStyle.Rounded;
        int y = 0;

        // ── General ──
        Add(new Label() { Text = "── General ─────────────────────────────────────────────────", X = 1, Y = y, ColorScheme = Theme.SectionHeader });
        y += 1;

        var autoSyncCheck = new CheckBox() { Text = "Auto-sync on startup", CheckedState = userSettings.AutoSyncOnStartup ? CheckState.Checked : CheckState.UnChecked, X = 2, Y = y };
        autoSyncCheck.CheckedStateChanged += (sender, e) => { userSettings.AutoSyncOnStartup = autoSyncCheck.CheckedState == CheckState.Checked; userSettings.Save(); };
        Add(autoSyncCheck);
        y += 1;

        var autoInstallCheck = new CheckBox() { Text = "Auto-install updates on startup", CheckedState = userSettings.AutoInstallUpdates ? CheckState.Checked : CheckState.UnChecked, X = 2, Y = y };
        autoInstallCheck.CheckedStateChanged += (sender, e) => { userSettings.AutoInstallUpdates = autoInstallCheck.CheckedState == CheckState.Checked; userSettings.Save(); };
        Add(autoInstallCheck);
        y += 1;

        var sortTrackedCheck = new CheckBox() { Text = "Sort tracked playlists first", CheckedState = userSettings.SortTrackedFirst ? CheckState.Checked : CheckState.UnChecked, X = 2, Y = y };
        sortTrackedCheck.CheckedStateChanged += (sender, e) => { userSettings.SortTrackedFirst = sortTrackedCheck.CheckedState == CheckState.Checked; userSettings.Save(); };
        Add(sortTrackedCheck);
        y += 1;

        // Theme selector
        Add(new Label() { Text = "  Theme:", X = 1, Y = y });
        var themeNames = ThemePalette.AllNames;
        var currentIdx = Array.IndexOf(themeNames, Theme.CurrentName);
        if (currentIdx < 0) currentIdx = 0;
        var themeRadio = new RadioGroup()
        {
            RadioLabels = themeNames,
            X = 12, Y = y,
            SelectedItem = currentIdx,
        };
        themeRadio.SelectedItemChanged += (sender, e) =>
        {
            var name = themeNames[themeRadio.SelectedItem];
            userSettings.ThemeName = name;
            userSettings.Save();
            Theme.Apply(name);
            ReapplyAllSchemes(this);
            SetNeedsDraw();

            if (global::Terminal.Gui.Application.Top is MainWindow mw)
                mw.ReapplyTheme();
        };
        Add(themeRadio);
        y += themeNames.Length + 1;

        // ── Sync ──
        Add(new Label() { Text = "── Sync ────────────────────────────────────────────────────", X = 1, Y = y, ColorScheme = Theme.SectionHeader });
        y += 1;

        var likedPolicy = PlaylistPolicy.For(Domain.Enums.PlaylistKind.Liked);
        var cooldownText = likedPolicy.ManualCooldown is { } cd ? $"{cd.TotalHours:0}h" : "none";
        Add(new Label() { Text = $"  Liked Videos cooldown:  {cooldownText}  (large playlist guard)", X = 1, Y = y });
        y += 1;

        Add(new Label() { Text = $"  Auto-sync cooldown:    {SyncService.AutoSyncCooldown.TotalHours:0}h   (between background syncs)", X = 1, Y = y });
        y += 2;

        // ── Credentials ──
        Add(new Label() { Text = "── Credentials ─────────────────────────────────────────────", X = 1, Y = y, ColorScheme = Theme.SectionHeader });
        y += 1;

        Add(new Label() { Text = "  YouTube API Key:", X = 1, Y = y });
        var apiKeyField = new TextField() { Text = userSettings.YouTubeApiKey, X = 21, Y = y, Width = 42, Secret = true };
        apiKeyField.HasFocusChanged += (sender, e) =>
        {
            if (e.NewValue) return; // Only act when losing focus
            var newKey = apiKeyField.Text?.Trim() ?? "";
            if (newKey != userSettings.YouTubeApiKey)
            {
                userSettings.YouTubeApiKey = newKey;
                userSettings.Save();
                AppSettings.LoadApiKey(newKey);
            }
        };
        Add(apiKeyField);
        y += 1;
        Add(new Label() { Text = "  Optional. Overrides built-in key for offline profiles.", X = 1, Y = y, ColorScheme = Colors.ColorSchemes["Menu"] });
        y += 2;

        // ── Data ──
        Add(new Label() { Text = "── Data ────────────────────────────────────────────────────", X = 1, Y = y, ColorScheme = Theme.SectionHeader });
        y += 1;

        Add(new Label() { Text = "  Database:", X = 1, Y = y });
        var dbBtn = new Button() { Text = AppSettings.DbPath, X = 14, Y = y, ColorScheme = Colors.ColorSchemes["Menu"] };
        dbBtn.Accepting += (sender, e) => launcher?.OpenPath(AppSettings.DbPath);
        Add(dbBtn);
        y += 1;
        Add(new Label() { Text = "  Logs:", X = 1, Y = y });
        var logBtn = new Button() { Text = AppSettings.LogDir, X = 14, Y = y, ColorScheme = Colors.ColorSchemes["Menu"] };
        logBtn.Accepting += (sender, e) => launcher?.OpenPath(AppSettings.LogDir);
        Add(logBtn);
        y += 1;

        var purgeBtn = new Button() { Text = "Purge Deleted Videos", X = 2, Y = y, ColorScheme = Theme.Danger };
        purgeBtn.Accepting += async (sender, e) =>
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
                    await playlistRepo.PurgeDeletedVideosAsync(selectedPlaylist.Id).ConfigureAwait(false);
                    MessageBox.Query("Done", "Deleted videos purged.", "OK");
                }
                catch (Exception ex)
                {
                    MessageBox.Query("Error", "Purge failed: " + ex.Message, "OK");
                }
            }
        };

        var resetBtn = new Button() { Text = "Reset Database", X = 26, Y = y, ColorScheme = Theme.Danger };
        resetBtn.Accepting += (sender, e) =>
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
                    global::Terminal.Gui.Application.Top?.RequestStop();
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
        Add(new Label() { Text = "── About ───────────────────────────────────────────────────", X = 1, Y = y, ColorScheme = Theme.SectionHeader });
        y += 1;

        Add(new Label() { Text = $"  Version:  {UpdateService.GetCurrentVersion()}", X = 1, Y = y });
        y += 1;
        var updateNowBtn = new Button() { Text = "Update Now", X = 2, Y = y };
        updateNowBtn.Accepting += (sender, e) =>
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var update = await updateService.CheckForUpdateAsync().ConfigureAwait(false);
                    global::Terminal.Gui.Application.Invoke(() =>
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
                    global::Terminal.Gui.Application.Invoke(() =>
                        MessageBox.Query("Error", $"Update check failed: {ex.Message}", "OK"));
                }
            });
        };
        Add(updateNowBtn);

        var closeBtn = new Button() { Text = "Close", IsDefault = true };
        closeBtn.Accepting += (sender, e) => global::Terminal.Gui.Application.RequestStop();
        AddButton(closeBtn);
    }

    private static void ReapplyAllSchemes(View root)
    {
        root.ColorScheme = Colors.ColorSchemes["Dialog"];
        foreach (var view in root.Subviews)
        {
            if (view is Label lbl && lbl.Text?.ToString()?.StartsWith("──", StringComparison.Ordinal) == true)
                lbl.ColorScheme = Theme.SectionHeader;
            else if (view is Button btn && (btn.Text?.ToString()?.Contains("Purge", StringComparison.Ordinal) == true
                || btn.Text?.ToString()?.Contains("Reset", StringComparison.Ordinal) == true))
                btn.ColorScheme = Theme.Danger;
            else
                view.ColorScheme = Colors.ColorSchemes["Dialog"];

            ReapplyAllSchemes(view);
        }
    }
}
