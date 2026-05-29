using System.Diagnostics;
using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Application.Services;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Domain.Models;
using YTPlaylistTracker.Infrastructure.Update;

namespace YTPlaylistTracker.UI.Views;

public partial class MainWindow
{
    private async Task OnShowHistory()
    {
        if (_selectedProfile is null) return;
        try
        {
            var removedVideos = await playlistRepo.GetAllDeletedVideosAsync(_selectedProfile.Id).ConfigureAwait(false);
            var dialog = new RemovalHistoryDialog(removedVideos, browser);
            _app.Run(dialog);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to show removal history");
            Dialogs.Query(_app, "Error", "Failed to load history: " + ex.Message, "OK");
        }
    }

    private async Task OnExport()
    {
        if (_selectedProfile is null) return;

        try
        {
            var removedVideos = await playlistRepo.GetAllDeletedVideosAsync(_selectedProfile.Id).ConfigureAwait(false);
            if (removedVideos.Count == 0)
            {
                Dialogs.Query(_app, "Export", "No removed videos to export.", "OK");
                return;
            }

            var dialog = new Dialog() { Title = "", Width = 50, Height = 11 };
            dialog.Border!.Settings &= ~BorderSettings.Title;
            dialog.Add(new Label { Text = " Export Removed Videos", X = 0, Y = 0, Width = Dim.Fill(), SchemeName = Theme.SchemeFrame });
            var formatLabel = new Label() { Text = "Format:", X = 1, Y = 2 };
            var formatSelector = new OptionSelector()
            {
                Labels = new List<string> { "CSV", "JSON" },
                X = 12, Y = 2,
                Value = 0,
            };
            var pathLabel = new Label() { Text = "File:", X = 1, Y = 4 };
            var defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "ytpt-removed-videos");
            var pathField = new TextField() { Text = defaultPath, X = 12, Y = 4, Width = Dim.Fill(2) };
            var okBtn = new Button() { Text = "Export", IsDefault = true };
            var cancelBtn = new Button() { Text = "Cancel" };

            string? resultPath = null;
            int selectedFormat = 0;
            okBtn.Accepting += (sender, e) => { resultPath = pathField.Text; selectedFormat = (int)(formatSelector.Value ?? 0); _app.RequestStop(); };
            cancelBtn.Accepting += (sender, e) => _app.RequestStop();

            dialog.Add(formatLabel, formatSelector, pathLabel, pathField);
            dialog.AddButton(okBtn);
            dialog.AddButton(cancelBtn);
            _app.Run(dialog);

            if (resultPath is null) return;

            var ext = selectedFormat == 1 ? ".json" : ".csv";
            if (!resultPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                resultPath += ext;

            var entries = ExportService.BuildEntries(removedVideos);
            var content = selectedFormat == 1
                ? ExportService.ToJson(entries)
                : ExportService.ToCsv(entries);

            await File.WriteAllTextAsync(resultPath, content).ConfigureAwait(false);
            Dialogs.Query(_app, "Export Complete", $"Exported {entries.Count} removed videos to:\n{resultPath}", "OK");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Export failed");
            Dialogs.Query(_app, "Error", "Export failed: " + ex.Message, "OK");
        }
    }

    private async void OnSettings()
    {
        var settingsDialog = new SettingsDialog(playlistRepo, _selectedPlaylist, userSettings, updateService, browser, this);
        _app.Run(settingsDialog);

        // Capture results, then dispose on the UI thread (Run is synchronous) so the
        // dialog's Disposing handler fires and unsubscribes its app-level KeyDown hook.
        // Must happen before the ConfigureAwait(false) await below to avoid touching
        // Terminal.Gui from a background thread.
        var updateInfo = settingsDialog.UpdateRequested ? settingsDialog.UpdateInfo : null;
        settingsDialog.Dispose();

        if (updateInfo is not null)
        {
            PerformUpdateAndRestart(updateInfo);
            return;
        }

        ReapplyTheme();
        await RefreshPlaylistsAsync().ConfigureAwait(false);
        ApplyFilterAndSort();
    }

    private void OnUpdateCheck()
    {
        if (_updateInstalled)
        {
            RestartApp();
            return;
        }

        if (_latestUpdate is { IsUpdateAvailable: true })
        {
            var confirm = Dialogs.Query(_app, "Update Available",
                $"Update to v{_latestUpdate.LatestVersion}?\nThe app will need to restart after updating.",
                "Update", "Cancel");

            if (confirm == 0)
                PerformUpdateAndRestart(_latestUpdate);
        }
        else
        {
            var currentVersion = UpdateService.GetCurrentVersion();
            Dialogs.Query(_app, "Up to Date", $"You're on the latest version (v{currentVersion}).", "OK");
        }
    }

    internal void PerformUpdateAndRestart(UpdateInfo update)
    {
        ShowSpinner("Downloading update...");
        _ = Task.Run(async () =>
        {
            try
            {
                await updateService.ApplyUpdateAsync(update).ConfigureAwait(false);
                _app.Invoke(() =>
                {
                    HideSpinner();
                    SchemeName = Theme.SchemeUpdateInstalled;
                    SetNeedsDraw();
                    RestartApp();
                });
            }
            catch (UpdateException ex)
            {
                _app.Invoke(() =>
                {
                    HideSpinner();
                    var msg = ex.ManualDownloadUrl is not null
                        ? $"{ex.Message}\n\nDownload manually:\n{ex.ManualDownloadUrl}"
                        : ex.Message;
                    Dialogs.Query(_app, "Update Failed", msg, "OK");
                });
            }
        });
    }

    private void RestartApp()
    {
        var binaryPath = Environment.ProcessPath;
        if (binaryPath is not null)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = binaryPath,
                UseShellExecute = false,
            });
        }

        _app.RequestStop();
    }
}
