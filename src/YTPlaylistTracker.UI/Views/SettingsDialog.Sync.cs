using Terminal.Gui;
using YTPlaylistTracker.Application.Services;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Domain.Models;
using YTPlaylistTracker.Infrastructure.Configuration;

namespace YTPlaylistTracker.UI.Views;

public sealed partial class SettingsDialog
{
    private static View BuildSyncTab(IUserSettings userSettings)
    {
        var view = new View() { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        int y = 0;

        // Cooldowns
        var likedPolicy = PlaylistPolicy.For(Domain.Enums.PlaylistKind.Liked);
        var cooldownText = likedPolicy.ManualCooldown is { } cd ? $"{cd.TotalHours:0}h" : "none";
        view.Add(new Label() { Text = $"  Liked Videos cooldown:  {cooldownText}  (large playlist guard)", X = 1, Y = y });
        y += 1;

        view.Add(new Label() { Text = $"  Auto-sync cooldown:    {SyncService.AutoSyncCooldown.TotalHours:0}h   (between background syncs)", X = 1, Y = y });
        y += 2;

        // Credentials
        view.Add(new Label() { Text = "── Credentials ─────────────────────────────────────────────", X = 1, Y = y, ColorScheme = Theme.SectionHeader });
        y += 1;

        view.Add(new Label() { Text = "  YouTube API Key:", X = 1, Y = y });
        var apiKeyField = new TextField() { Text = userSettings.YouTubeApiKey, X = 21, Y = y, Width = 42, Secret = true };
        apiKeyField.HasFocusChanged += (sender, e) =>
        {
            if (e.NewValue) return;
            var newKey = apiKeyField.Text?.Trim() ?? "";
            if (newKey != userSettings.YouTubeApiKey)
            {
                userSettings.YouTubeApiKey = newKey;
                userSettings.Save();
                AppSettings.LoadApiKey(newKey);
            }
        };
        view.Add(apiKeyField);
        y += 1;
        view.Add(new Label() { Text = "  Optional. Overrides built-in key for offline profiles.", X = 1, Y = y, ColorScheme = Colors.ColorSchemes["Menu"] });

        return view;
    }
}
