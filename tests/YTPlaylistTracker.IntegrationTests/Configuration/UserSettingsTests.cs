using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Infrastructure.Configuration;

namespace YTPlaylistTracker.IntegrationTests.Configuration;

[NotInParallel(nameof(UserSettingsTests))]
public class UserSettingsTests
{
    private string _tempDir = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ytpt-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        // Ensure app data directory exists for Save() calls
        AppSettings.EnsureDirectories();
    }

    [Test]
    public async Task DefaultSettings_AutoSyncIsTrue()
    {
        var settings = new UserSettings();
        await Assert.That(settings.AutoSyncOnStartup).IsTrue();
    }

    [Test]
    public async Task Save_And_Load_RoundTrips()
    {
        var settings = new UserSettings { AutoSyncOnStartup = false };
        settings.Save();

        var loaded = UserSettings.Load();
        await Assert.That(loaded.AutoSyncOnStartup).IsFalse();

        // Restore default
        settings.AutoSyncOnStartup = true;
        settings.Save();

        var restored = UserSettings.Load();
        await Assert.That(restored.AutoSyncOnStartup).IsTrue();
    }

    [Test]
    public async Task Load_MissingFile_ReturnsDefaults()
    {
        var settingsPath = Path.Combine(AppSettings.AppDataDir, "settings.json");
        var existed = File.Exists(settingsPath);
        string? backup = null;

        if (existed)
        {
            backup = settingsPath + ".bak";
            File.Move(settingsPath, backup, overwrite: true);
        }

        try
        {
            var loaded = UserSettings.Load();
            await Assert.That(loaded.AutoSyncOnStartup).IsTrue();
        }
        finally
        {
            if (backup != null)
                File.Move(backup, settingsPath, overwrite: true);
        }
    }

    [Test]
    public async Task Load_MalformedJson_ReturnsDefaults()
    {
        var settingsPath = Path.Combine(AppSettings.AppDataDir, "settings.json");
        var existed = File.Exists(settingsPath);
        string? backup = null;

        if (existed)
        {
            backup = settingsPath + ".bak";
            File.Move(settingsPath, backup, overwrite: true);
        }

        try
        {
            File.WriteAllText(settingsPath, "not valid json {{{");

            var loaded = UserSettings.Load();
            await Assert.That(loaded.AutoSyncOnStartup).IsTrue(); // falls back to defaults
        }
        finally
        {
            if (backup != null)
                File.Move(backup, settingsPath, overwrite: true);
            else if (File.Exists(settingsPath))
                File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task Save_Implements_IUserSettings()
    {
        IUserSettings settings = new UserSettings();
        settings.AutoSyncOnStartup = false;
        settings.Save();

        var loaded = UserSettings.Load();
        await Assert.That(loaded.AutoSyncOnStartup).IsFalse();

        // Restore
        settings.AutoSyncOnStartup = true;
        settings.Save();
    }

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }
}
