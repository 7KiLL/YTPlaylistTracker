using Microsoft.Extensions.Logging;
using Terminal.Gui;
using YTPlaylistTracker.Application.Services;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Infrastructure.Update;

namespace YTPlaylistTracker.UI.Views;

public partial class MainWindow
{
    private async Task OnShowHistory()
    {
        if (_selectedProfile is null) return;
        try
        {
            var removedVideos = await playlistRepo.GetAllDeletedVideosAsync(_selectedProfile.Id);
            var dialog = new RemovalHistoryDialog(removedVideos);
            global::Terminal.Gui.Application.Run(dialog);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to show removal history");
            MessageBox.Query("Error", "Failed to load history: " + ex.Message, "OK");
        }
    }

    private async Task OnExport()
    {
        if (_selectedProfile is null) return;

        try
        {
            var removedVideos = await playlistRepo.GetAllDeletedVideosAsync(_selectedProfile.Id);
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
            logger.LogError(ex, "Export failed");
            MessageBox.Query("Error", "Export failed: " + ex.Message, "OK");
        }
    }

    private async void OnSettings()
    {
        var settingsDialog = new SettingsDialog(playlistRepo, _selectedPlaylist, userSettings, updateService, browser);
        global::Terminal.Gui.Application.Run(settingsDialog);
        await RefreshPlaylistsAsync();
        ApplyFilterAndSort();
    }

    private void OnUpdateCheck()
    {
        if (_latestUpdate is { IsUpdateAvailable: true })
        {
            var confirm = MessageBox.Query("Update Available",
                $"Update to v{_latestUpdate.LatestVersion}?\nThe app will need to restart after updating.",
                "Update", "Cancel");

            if (confirm == 0)
            {
                ShowSpinner("Downloading update...");
                Task.Run(async () =>
                {
                    try
                    {
                        var message = await updateService.ApplyUpdateAsync(_latestUpdate);
                        global::Terminal.Gui.Application.MainLoop.Invoke(() =>
                        {
                            HideSpinner();
                            MessageBox.Query("Update Complete", message, "OK");
                            if (OperatingSystem.IsWindows())
                                global::Terminal.Gui.Application.RequestStop();
                        });
                    }
                    catch (UpdateException ex)
                    {
                        global::Terminal.Gui.Application.MainLoop.Invoke(() =>
                        {
                            HideSpinner();
                            var msg = ex.ManualDownloadUrl is not null
                                ? $"{ex.Message}\n\nDownload manually:\n{ex.ManualDownloadUrl}"
                                : ex.Message;
                            MessageBox.Query("Update Failed", msg, "OK");
                        });
                    }
                });
            }
        }
        else
        {
            var currentVersion = UpdateService.GetCurrentVersion();
            MessageBox.Query("Up to Date", $"You're on the latest version (v{currentVersion}).", "OK");
        }
    }
}
