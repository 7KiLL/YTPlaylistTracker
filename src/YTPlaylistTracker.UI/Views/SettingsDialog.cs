using Terminal.Gui;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Infrastructure.Configuration;

namespace YTPlaylistTracker.UI.Views;

public class SettingsDialog : Dialog
{
    public SettingsDialog(IPlaylistRepository playlistRepo, Playlist? selectedPlaylist)
        : base("Settings", 60, 18)
    {
        var dbPathLabel = new Label("Database path:") { X = 1, Y = 1 };
        var dbPathValue = new Label(AppSettings.DbPath) { X = 1, Y = 2 };

        var logPathLabel = new Label("Log directory:") { X = 1, Y = 4 };
        var logPathValue = new Label(AppSettings.LogDir) { X = 1, Y = 5 };

        var autoSyncCheck = new CheckBox("Auto-sync on startup", AppSettings.AutoSyncOnStartup) { X = 1, Y = 7 };
        autoSyncCheck.Toggled += (prev) =>
        {
            AppSettings.AutoSyncOnStartup = autoSyncCheck.Checked;
            AppSettings.SaveSettings();
        };

        var purgeBtn = new Button("Purge Deleted Videos") { X = 1, Y = 9 };
        purgeBtn.Clicked += async () =>
        {
            try
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
                    await playlistRepo.PurgeDeletedVideosAsync(selectedPlaylist.Id);
                    MessageBox.Query("Done", "Deleted videos purged.", "OK");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Query("Error", "Purge failed: " + ex.Message, "OK");
            }
        };

        var resetBtn = new Button("Reset Database") { X = 1, Y = 11 };
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
                    global::Terminal.Gui.Application.Top.RequestStop(); // close main window (outermost)
                    global::Terminal.Gui.Application.RequestStop(); // close this dialog
                }
                catch (Exception ex)
                {
                    MessageBox.Query("Error", "Failed to delete database: " + ex.Message, "OK");
                }
            }
        };

        var closeBtn = new Button("Close", true);
        closeBtn.Clicked += () => global::Terminal.Gui.Application.RequestStop();

        Add(dbPathLabel, dbPathValue, logPathLabel, logPathValue, autoSyncCheck, purgeBtn, resetBtn);
        AddButton(closeBtn);
    }
}
