using System.Text.Json;
using YTPlaylistTracker.Domain.Interfaces;

namespace YTPlaylistTracker.Infrastructure.Configuration;

public class UserSettings : IUserSettings
{
    private static string SettingsPath => Path.Combine(AppSettings.AppDataDir, "settings.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public bool AutoSyncOnStartup { get; set; } = true;
    public bool AutoInstallUpdates { get; set; }
    public bool SortTrackedFirst { get; set; } = true;
    public string ThemeName { get; set; } = "";

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var json = JsonSerializer.Serialize(new
        {
            autoSyncOnStartup = AutoSyncOnStartup,
            autoInstallUpdates = AutoInstallUpdates,
            sortTrackedFirst = SortTrackedFirst,
            themeName = ThemeName,
        }, JsonOptions);
        File.WriteAllText(SettingsPath, json);

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            File.SetUnixFileMode(SettingsPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public static UserSettings Load()
    {
        var settings = new UserSettings();
        if (!File.Exists(SettingsPath)) return settings;

        try
        {
            var doc = JsonDocument.Parse(File.ReadAllText(SettingsPath));
            if (doc.RootElement.TryGetProperty("autoSyncOnStartup", out var val))
                settings.AutoSyncOnStartup = val.GetBoolean();
            if (doc.RootElement.TryGetProperty("autoInstallUpdates", out var val4))
                settings.AutoInstallUpdates = val4.GetBoolean();
            if (doc.RootElement.TryGetProperty("sortTrackedFirst", out var val3))
                settings.SortTrackedFirst = val3.GetBoolean();
            if (doc.RootElement.TryGetProperty("themeName", out var val5))
                settings.ThemeName = val5.GetString() ?? "";
        }
        catch
        {
            // Malformed settings — use defaults
        }

        return settings;
    }
}
