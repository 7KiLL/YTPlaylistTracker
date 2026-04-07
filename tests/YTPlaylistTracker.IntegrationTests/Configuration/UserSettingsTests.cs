using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Infrastructure.Configuration;

namespace YTPlaylistTracker.IntegrationTests.Configuration;

public class UserSettingsTests : IDisposable
{
    private readonly string _tempDir;

    public UserSettingsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ytpt-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        // Ensure app data directory exists for Save() calls
        AppSettings.EnsureDirectories();
    }

    [Fact]
    public void DefaultSettings_AutoSyncIsTrue()
    {
        var settings = new UserSettings();
        Assert.True(settings.AutoSyncOnStartup);
    }

    [Fact]
    public void Save_And_Load_RoundTrips()
    {
        var settings = new UserSettings { AutoSyncOnStartup = false };
        settings.Save();

        var loaded = UserSettings.Load();
        Assert.False(loaded.AutoSyncOnStartup);

        // Restore default
        settings.AutoSyncOnStartup = true;
        settings.Save();

        var restored = UserSettings.Load();
        Assert.True(restored.AutoSyncOnStartup);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var settingsPath = Path.Combine(AppSettings.AppDataDir, "settings.json");
        var existed = File.Exists(settingsPath);
        string? backup = null;

        if (existed)
        {
            backup = settingsPath + ".bak";
            File.Move(settingsPath, backup);
        }

        try
        {
            var loaded = UserSettings.Load();
            Assert.True(loaded.AutoSyncOnStartup);
        }
        finally
        {
            if (backup != null)
                File.Move(backup, settingsPath);
        }
    }

    [Fact]
    public void Load_MalformedJson_ReturnsDefaults()
    {
        var settingsPath = Path.Combine(AppSettings.AppDataDir, "settings.json");
        var existed = File.Exists(settingsPath);
        string? backup = null;

        if (existed)
        {
            backup = settingsPath + ".bak";
            File.Move(settingsPath, backup);
        }

        try
        {
            File.WriteAllText(settingsPath, "not valid json {{{");

            var loaded = UserSettings.Load();
            Assert.True(loaded.AutoSyncOnStartup); // falls back to defaults
        }
        finally
        {
            if (backup != null)
                File.Move(backup, settingsPath);
            else if (File.Exists(settingsPath))
                File.Delete(settingsPath);
        }
    }

    [Fact]
    public void Save_Implements_IUserSettings()
    {
        IUserSettings settings = new UserSettings();
        settings.AutoSyncOnStartup = false;
        settings.Save();

        var loaded = UserSettings.Load();
        Assert.False(loaded.AutoSyncOnStartup);

        // Restore
        settings.AutoSyncOnStartup = true;
        settings.Save();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }
}
