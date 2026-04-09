using Terminal.Gui;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Infrastructure.Update;

namespace YTPlaylistTracker.UI.Views;

public sealed partial class SettingsDialog
{
    private View BuildGeneralTab(IUserSettings userSettings, IUpdateService updateService, MainWindow? mainWindow)
    {
        var view = new View() { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        int y = 0;

        var autoSyncCheck = new CheckBox() { Text = "Auto-sync on startup", CheckedState = userSettings.AutoSyncOnStartup ? CheckState.Checked : CheckState.UnChecked, X = 2, Y = y };
        autoSyncCheck.CheckedStateChanged += (sender, e) => { userSettings.AutoSyncOnStartup = autoSyncCheck.CheckedState == CheckState.Checked; userSettings.Save(); };
        view.Add(autoSyncCheck);
        y += 1;

        var autoInstallCheck = new CheckBox() { Text = "Auto-install updates on startup", CheckedState = userSettings.AutoInstallUpdates ? CheckState.Checked : CheckState.UnChecked, X = 2, Y = y };
        autoInstallCheck.CheckedStateChanged += (sender, e) => { userSettings.AutoInstallUpdates = autoInstallCheck.CheckedState == CheckState.Checked; userSettings.Save(); };
        view.Add(autoInstallCheck);
        y += 1;

        var sortTrackedCheck = new CheckBox() { Text = "Sort tracked playlists first", CheckedState = userSettings.SortTrackedFirst ? CheckState.Checked : CheckState.UnChecked, X = 2, Y = y };
        sortTrackedCheck.CheckedStateChanged += (sender, e) => { userSettings.SortTrackedFirst = sortTrackedCheck.CheckedState == CheckState.Checked; userSettings.Save(); };
        view.Add(sortTrackedCheck);
        y += 2;

        // Theme selector
        view.Add(new Label() { Text = "── Theme ───────────────────────────────────────────────────", X = 1, Y = y, ColorScheme = Theme.SectionHeader });
        y += 1;

        var themeNames = ThemePalette.AllNames;
        var currentIdx = Array.IndexOf(themeNames, Theme.CurrentName);
        if (currentIdx < 0) currentIdx = 0;
        var themeRadio = new RadioGroup()
        {
            RadioLabels = themeNames,
            X = 2, Y = y,
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

            mainWindow?.ReapplyTheme();
        };
        view.Add(themeRadio);
        y += themeNames.Length + 1;

        // About
        view.Add(new Label() { Text = "── About ───────────────────────────────────────────────────", X = 1, Y = y, ColorScheme = Theme.SectionHeader });
        y += 1;

        view.Add(new Label() { Text = $"  Version:  {UpdateService.GetCurrentVersion()}", X = 1, Y = y });
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
                            Dialogs.Query("Up to Date", $"You're on the latest version ({update.CurrentVersion}).", "OK");
                        }
                    });
                }
                catch (Exception ex)
                {
                    global::Terminal.Gui.Application.Invoke(() =>
                        Dialogs.Query("Error", $"Update check failed: {ex.Message}", "OK"));
                }
            });
        };
        view.Add(updateNowBtn);

        return view;
    }
}
