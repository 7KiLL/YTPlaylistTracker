using System.Diagnostics;
using System.Text.Json;

namespace YTPlaylistTracker.Infrastructure.Configuration;

public class AppSettings
{
    public int SyncIntervalHours { get; set; } = 6;
    public bool AutoSyncOnStartup { get; set; } = true;
    public bool BackgroundSyncEnabled { get; set; } = true;

    public static string AppDataDir
    {
        get
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(baseDir, "ytpt");
        }
    }

    public static string DbPath => Path.Combine(AppDataDir, "tracker.db");
    public static string LogDir => Path.Combine(AppDataDir, "logs");
    public static string OAuthTokenDir => Path.Combine(AppDataDir, "oauth-tokens");
    public static string CredentialsPath => Path.Combine(AppDataDir, "credentials.json");

    // OAuth credentials — resolved in order: env vars → local credentials.json → build-time constants
    public static string OAuthClientId { get; set; } = "";
    public static string OAuthClientSecret { get; set; } = "";

    /// <summary>
    /// Save OAuth client ID/secret to local credentials.json (gitignored, owner-only permissions).
    /// Called during `ytpt login` so subsequent runs don't need env vars.
    /// </summary>
    public static void SaveCredentials(string clientId, string clientSecret)
    {
        var json = JsonSerializer.Serialize(new { clientId, clientSecret });
        File.WriteAllText(CredentialsPath, json);

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            File.SetUnixFileMode(CredentialsPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    /// <summary>
    /// Load OAuth credentials from all sources (env vars → credentials.json → build-time constants).
    /// </summary>
    public static void LoadCredentials()
    {
        // 1. Env vars take priority
        var envId = Environment.GetEnvironmentVariable("YTPT_CLIENT_ID");
        var envSecret = Environment.GetEnvironmentVariable("YTPT_CLIENT_SECRET");
        if (!string.IsNullOrWhiteSpace(envId) && !string.IsNullOrWhiteSpace(envSecret))
        {
            OAuthClientId = envId;
            OAuthClientSecret = envSecret;
            return;
        }

        // 2. Local credentials.json
        if (File.Exists(CredentialsPath))
        {
            try
            {
                var doc = JsonDocument.Parse(File.ReadAllText(CredentialsPath));
                var id = doc.RootElement.GetProperty("clientId").GetString();
                var secret = doc.RootElement.GetProperty("clientSecret").GetString();
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(secret))
                {
                    OAuthClientId = id;
                    OAuthClientSecret = secret;
                    return;
                }
            }
            catch (Exception ex)
            {
                // Malformed credentials.json — fall through to build-time constants
                Debug.WriteLine($"Failed to parse {CredentialsPath}: {ex.Message}");
            }
        }

        // 3. Build-time constants already set via Program.cs → BuildConstants
    }

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDataDir);
        Directory.CreateDirectory(LogDir);
        Directory.CreateDirectory(OAuthTokenDir);

        // Restrict permissions on sensitive directories (Linux/macOS)
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            File.SetUnixFileMode(AppDataDir, ownerOnly);
            File.SetUnixFileMode(LogDir, ownerOnly);
            File.SetUnixFileMode(OAuthTokenDir, ownerOnly);
        }
    }

    /// <summary>
    /// Set restrictive permissions on the database file after creation.
    /// </summary>
    public static void SecureDbFile()
    {
        if ((OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) && File.Exists(DbPath))
        {
            File.SetUnixFileMode(DbPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
