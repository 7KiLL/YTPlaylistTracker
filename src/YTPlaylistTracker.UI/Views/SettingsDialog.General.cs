using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Infrastructure.Update;

namespace YTPlaylistTracker.UI.Views;

public sealed partial class SettingsDialog
{
    private View BuildGeneralTab(IUserSettings userSettings, IUpdateService updateService, MainWindow? mainWindow)
    {
        var view = new View() { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        int y = 0;

        var autoSyncCheck = new CheckBox() { Text = "Auto-sync on startup", Value = userSettings.AutoSyncOnStartup ? CheckState.Checked : CheckState.UnChecked, X = 2, Y = y };
        autoSyncCheck.ValueChanged += (sender, e) => { userSettings.AutoSyncOnStartup = autoSyncCheck.Value == CheckState.Checked; userSettings.Save(); };
        view.Add(autoSyncCheck);
        y += 1;

        var autoInstallCheck = new CheckBox() { Text = "Auto-install updates on startup", Value = userSettings.AutoInstallUpdates ? CheckState.Checked : CheckState.UnChecked, X = 2, Y = y };
        autoInstallCheck.ValueChanged += (sender, e) => { userSettings.AutoInstallUpdates = autoInstallCheck.Value == CheckState.Checked; userSettings.Save(); };
        view.Add(autoInstallCheck);
        y += 1;

        var sortTrackedCheck = new CheckBox() { Text = "Sort tracked playlists first", Value = userSettings.SortTrackedFirst ? CheckState.Checked : CheckState.UnChecked, X = 2, Y = y };
        sortTrackedCheck.ValueChanged += (sender, e) => { userSettings.SortTrackedFirst = sortTrackedCheck.Value == CheckState.Checked; userSettings.Save(); };
        view.Add(sortTrackedCheck);
        y += 2;

        // Theme selector
        view.Add(new Label() { Text = "── Theme ───────────────────────────────────────────────────", X = 1, Y = y, SchemeName = Theme.SchemeSectionHeader });
        y += 1;

        var themeNames = ThemePalette.AllNames;
        var currentIdx = Array.IndexOf(themeNames, Theme.CurrentName);
        if (currentIdx < 0) currentIdx = 0;
        var themeSelector = new OptionSelector()
        {
            Labels = themeNames.ToList(),
            X = 2, Y = y,
            Value = currentIdx,
        };
        themeSelector.ValueChanged += (sender, e) =>
        {
            var name = themeNames[(int)(themeSelector.Value ?? 0)];
            userSettings.ThemeName = name;
            userSettings.Save();
            Theme.Apply(name);
            ReapplyAllSchemes(this);
            SetNeedsDraw();

            mainWindow?.ReapplyTheme();
        };
        view.Add(themeSelector);
        y += themeNames.Length + 1;

        // About
        view.Add(new Label() { Text = "── About ───────────────────────────────────────────────────", X = 1, Y = y, SchemeName = Theme.SchemeSectionHeader });
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
                    TGuiApp.Invoke(() =>
                    {
                        if (update.IsUpdateAvailable)
                        {
                            UpdateRequested = true;
                            UpdateInfo = update;
                            TGuiApp.RequestStop();
                        }
                        else
                        {
                            Dialogs.Query("Up to Date", $"You're on the latest version ({update.CurrentVersion}).", "OK");
                        }
                    });
                }
                catch (Exception ex)
                {
                    TGuiApp.Invoke(() =>
                        Dialogs.Query("Error", $"Update check failed: {ex.Message}", "OK"));
                }
            });
        };
        view.Add(updateNowBtn);

        return view;
    }
}
