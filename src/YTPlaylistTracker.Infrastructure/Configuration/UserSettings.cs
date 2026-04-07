using System.Text.Json;
using YTPlaylistTracker.Domain.Interfaces;

namespace YTPlaylistTracker.Infrastructure.Configuration;

public class UserSettings : IUserSettings
{
    private static string SettingsPath => Path.Combine(AppSettings.AppDataDir, "settings.json");

    public bool AutoSyncOnStartup { get; set; } = true;
    public bool CheckForUpdatesOnStartup { get; set; } = true;

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var json = JsonSerializer.Serialize(new { autoSyncOnStartup = AutoSyncOnStartup, checkForUpdatesOnStartup = CheckForUpdatesOnStartup },
            new JsonSerializerOptions { WriteIndented = true });
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
            if (doc.RootElement.TryGetProperty("checkForUpdatesOnStartup", out var val2))
                settings.CheckForUpdatesOnStartup = val2.GetBoolean();
        }
        catch
        {
            // Malformed settings — use defaults
        }

        return settings;
    }
}
