using Terminal.Gui;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Infrastructure.Configuration;
using YTPlaylistTracker.Infrastructure.Platform;

namespace YTPlaylistTracker.UI.Views;

public sealed partial class SettingsDialog
{
    private static View BuildStorageTab(IPlaylistRepository playlistRepo,
        Playlist? selectedPlaylist, ISystemLauncher? launcher)
    {
        var view = new View() { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        int y = 0;

        // Paths
        view.Add(new Label() { Text = "  Database:", X = 1, Y = y });
        var dbBtn = new Button() { Text = AppSettings.DbPath, X = 14, Y = y, ColorScheme = Colors.ColorSchemes["Menu"] };
        dbBtn.Accepting += (sender, e) => launcher?.OpenPath(AppSettings.DbPath);
        view.Add(dbBtn);
        y += 1;

        view.Add(new Label() { Text = "  Logs:", X = 1, Y = y });
        var logBtn = new Button() { Text = AppSettings.LogDir, X = 14, Y = y, ColorScheme = Colors.ColorSchemes["Menu"] };
        logBtn.Accepting += (sender, e) => launcher?.OpenPath(AppSettings.LogDir);
        view.Add(logBtn);
        y += 2;

        // Danger zone
        view.Add(new Label() { Text = "── Danger Zone ─────────────────────────────────────────────", X = 1, Y = y, ColorScheme = Theme.SectionHeader });
        y += 1;

        var purgeBtn = new Button() { Text = "Purge Deleted Videos", X = 2, Y = y, ColorScheme = Theme.Danger };
        purgeBtn.Accepting += (sender, e) =>
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
                    playlistRepo.PurgeDeletedVideosAsync(selectedPlaylist.Id).GetAwaiter().GetResult();
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
                    global::Terminal.Gui.Application.RequestStop();
                }
                catch (Exception ex)
                {
                    MessageBox.Query("Error", "Failed to delete database: " + ex.Message, "OK");
                }
            }
        };
        view.Add(purgeBtn, resetBtn);

        return view;
    }
}
