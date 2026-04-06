using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Terminal.Gui;
using YTPlaylistTracker.Application.Services;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Infrastructure.Configuration;
using YTPlaylistTracker.Infrastructure.Data;
using YTPlaylistTracker.Infrastructure.Platform;
using YTPlaylistTracker.Infrastructure.YouTube;
using YTPlaylistTracker.UI;
using YTPlaylistTracker.UI.Views;
using CliCommand = System.CommandLine.Command;
using CliRootCommand = System.CommandLine.RootCommand;
using System.CommandLine;

// Bootstrap
AppSettings.OAuthClientId = BuildConstants.OAuthClientId;
AppSettings.OAuthClientSecret = BuildConstants.OAuthClientSecret;
AppSettings.EnsureDirectories();
AppSettings.LoadCredentials();

var isVerbose = args.Contains("--verbose") || args.Contains("-v");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(isVerbose ? Serilog.Events.LogEventLevel.Debug : Serilog.Events.LogEventLevel.Information)
    .WriteTo.File(
        Path.Combine(AppSettings.LogDir, "ytpt-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

ServiceProvider sp = null!;
try
{
// DI — no GenericHost
var services = new ServiceCollection();
services.AddLogging(lb => lb.ClearProviders().AddSerilog());
services.AddDbContext<AppDbContext>(opts => opts.UseSqlite($"Data Source={AppSettings.DbPath}"));
services.AddScoped<IProfileRepository, ProfileRepository>();
services.AddScoped<IPlaylistRepository, PlaylistRepository>();
services.AddScoped<ISyncService, SyncService>();
services.AddSingleton<IBrowserLauncher, BrowserLauncher>();
services.AddSingleton<Lazy<IYouTubeApiService>>(sp =>
    new Lazy<IYouTubeApiService>(() =>
    {
        var logger = sp.GetRequiredService<ILogger<YouTubeApiService>>();
        if (YouTubeApiService.HasStoredToken("default"))
            return YouTubeApiService.CreateWithEmbeddedOAuthAsync("default", logger).GetAwaiter().GetResult();
        var credPath = Environment.GetEnvironmentVariable("YTPT_OAUTH_CREDENTIALS")
                       ?? Path.Combine(AppSettings.AppDataDir, "client_secrets.json");
        if (File.Exists(credPath))
            return YouTubeApiService.CreateWithOAuthAsync(credPath, "default", logger).GetAwaiter().GetResult();
        var apiKey = Environment.GetEnvironmentVariable("YOUTUBE_API_KEY");
        if (!string.IsNullOrWhiteSpace(apiKey))
            return YouTubeApiService.CreateWithApiKey(apiKey, logger);
        Log.Warning("Not logged in. Run 'ytpt login' or set YOUTUBE_API_KEY.");
        return YouTubeApiService.CreateWithApiKey("", logger);
    }));
services.AddSingleton<IYouTubeApiService>(sp =>
    new LazyYouTubeApiProxy(sp.GetRequiredService<Lazy<IYouTubeApiService>>()));

sp = services.BuildServiceProvider();

using (var scope = sp.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Handle upgrade from v0.1.0 (EnsureCreated) to migrations
    var conn = db.Database.GetDbConnection();
    await conn.OpenAsync();
    await using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'";
        var hasMigrationHistory = await cmd.ExecuteScalarAsync() != null;

        if (!hasMigrationHistory)
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Profiles'";
            var hasExistingTables = await cmd.ExecuteScalarAsync() != null;

            if (hasExistingTables)
            {
                // Legacy DB from v0.1.0 — stamp InitialCreate as already applied
                var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
                var initialMigration = pendingMigrations.FirstOrDefault(m => m.EndsWith("_InitialCreate"));
                if (initialMigration != null)
                {
                    await db.Database.ExecuteSqlRawAsync(
                        "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL, \"ProductVersion\" TEXT NOT NULL, PRIMARY KEY(\"MigrationId\"))");

                    var efVersion = typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "10.0.5";
                    await db.Database.ExecuteSqlRawAsync(
                        "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({0}, {1})",
                        initialMigration, efVersion);
                    Log.Information("Upgraded legacy database: stamped {Migration}", initialMigration);
                }
            }
        }
    }

    await db.Database.MigrateAsync();
    AppSettings.SecureDbFile();
}

// --- CLI ---
var root = new CliRootCommand("ytpt — YouTube Playlist Tracker");

var verboseOpt = new System.CommandLine.Option<bool>("--verbose", "-v") { Description = "Enable debug logging" };
root.Add(verboseOpt);

// ui
var uiCmd = new CliCommand("ui", "Launch the interactive TUI");
uiCmd.SetAction(async (_, _) => await RunUi());
root.Add(uiCmd);

// sync [playlist-id]
var syncArg = new System.CommandLine.Argument<string?>("playlist-id") { Description = "Playlist ID. Omit to sync all.", Arity = ArgumentArity.ZeroOrOne };
var syncCmd = new CliCommand("sync", "Sync playlists from YouTube") { syncArg };
syncCmd.SetAction(async (result, _) =>
{
    var playlistId = result.GetValue(syncArg);
    await RunSync(playlistId);
});
root.Add(syncCmd);

// status
var statusCmd = new CliCommand("status", "Show tracking summary");
statusCmd.SetAction(async (_, _) => await RunStatus());
root.Add(statusCmd);

// login
var loginCmd = new CliCommand("login", "Sign in with Google (opens browser)");
loginCmd.SetAction(async (_, _) => await RunLogin());
root.Add(loginCmd);

// logout
var logoutCmd = new CliCommand("logout", "Sign out and remove stored tokens");
logoutCmd.SetAction((_, _) => { RunLogout(); return Task.CompletedTask; });
root.Add(logoutCmd);

// reset
var resetYesOpt = new System.CommandLine.Option<bool>("--yes", "-y") { Description = "Skip confirmation" };
var resetCmd = new CliCommand("reset", "Delete database and start fresh") { resetYesOpt };
resetCmd.SetAction((result, _) =>
{
    var yes = result.GetValue(resetYesOpt);
    if (!yes)
    {
        Console.Write($"Delete {AppSettings.DbPath}? [y/N] ");
        var key = Console.ReadLine()?.Trim().ToLower();
        if (key is not "y" and not "yes") { Console.WriteLine("Cancelled."); return Task.CompletedTask; }
    }
    if (File.Exists(AppSettings.DbPath))
    {
        File.Delete(AppSettings.DbPath);
        Console.WriteLine("Database deleted: " + AppSettings.DbPath);
    }
    else
    {
        Console.WriteLine("No database found.");
    }
    return Task.CompletedTask;
});
root.Add(resetCmd);

// Default: launch TUI
root.SetAction(async (_, _) => await RunUi());

return await root.Parse(args).InvokeAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
    Console.Error.WriteLine("ytpt encountered an unexpected error.");
    Console.Error.WriteLine($"Details: {ex.Message}");
    Console.Error.WriteLine($"Logs: {Path.Combine(AppSettings.LogDir, "ytpt-*.log")}");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

// --- Implementations ---

async Task RunUi()
{
    using var scope = sp.CreateScope();
    var s = scope.ServiceProvider;
    Application.Init();
    Theme.Apply();
    try
    {
        var mainWindow = new MainWindow(
            s.GetRequiredService<IPlaylistRepository>(),
            s.GetRequiredService<IProfileRepository>(),
            s.GetRequiredService<ISyncService>(),
            s.GetRequiredService<IYouTubeApiService>(),
            sp.GetRequiredService<IBrowserLauncher>(),
            s.GetRequiredService<ILogger<MainWindow>>());
        await mainWindow.InitializeAsync();
        Application.Run(mainWindow);
        mainWindow.Dispose();
    }
    finally
    {
        Application.Shutdown();
    }
}

async Task RunSync(string? playlistId)
{
    using var scope = sp.CreateScope();
    var s = scope.ServiceProvider;
    var syncService = s.GetRequiredService<ISyncService>();
    var playlistRepo = s.GetRequiredService<IPlaylistRepository>();
    var profileRepo = s.GetRequiredService<IProfileRepository>();

    var profile = await profileRepo.GetDefaultAsync();
    if (profile is null) { Console.Error.WriteLine("No profile configured. Run 'ytpt ui' first."); return; }

    if (playlistId is not null)
    {
        var playlists = await playlistRepo.GetByProfileAsync(profile.Id);
        var playlist = playlists.FirstOrDefault(p => p.YouTubePlaylistId == playlistId);
        if (playlist is null) { Console.Error.WriteLine($"Playlist {playlistId} not found."); return; }
        var result = await syncService.SyncPlaylistAsync(playlist);
        Console.WriteLine($"Synced: +{result.Added} added, -{result.Removed} removed, ~{result.Updated} updated");
    }
    else
    {
        var results = await syncService.SyncAllTrackedAsync(profile.Id);
        foreach (var (id, result) in results)
            Console.WriteLine($"Playlist {id}: +{result.Added} -{result.Removed} ~{result.Updated}");
        Console.WriteLine($"Total: {results.Count} playlists synced");
    }
}

async Task RunStatus()
{
    using var scope = sp.CreateScope();
    var s = scope.ServiceProvider;
    var profileRepo = s.GetRequiredService<IProfileRepository>();
    var playlistRepo = s.GetRequiredService<IPlaylistRepository>();

    var profiles = await profileRepo.GetAllAsync();
    foreach (var profile in profiles)
    {
        Console.WriteLine($"Profile: {profile.Name}{(profile.IsDefault ? " (default)" : "")}");
        var playlists = await playlistRepo.GetByProfileAsync(profile.Id);
        foreach (var pl in playlists)
        {
            var videos = await playlistRepo.GetVideosAsync(pl.Id);
            var active = videos.Count(v => v.DeletedAt == null);
            var deleted = videos.Count(v => v.DeletedAt != null);
            Console.WriteLine($"  {(pl.IsTracked ? "✓" : " ")} {pl.Title ?? pl.YouTubePlaylistId} — {active} active, {deleted} removed, last sync: {pl.LastSyncedAt?.ToString("g") ?? "never"}");
        }
    }
}

async Task RunLogin()
{
    if (string.IsNullOrWhiteSpace(AppSettings.OAuthClientId) || string.IsNullOrWhiteSpace(AppSettings.OAuthClientSecret))
    {
        Console.Error.WriteLine("OAuth client ID/secret not configured.");
        Console.Error.WriteLine("Set YTPT_CLIENT_ID and YTPT_CLIENT_SECRET env vars first.");
        return;
    }
    AppSettings.SaveCredentials(AppSettings.OAuthClientId, AppSettings.OAuthClientSecret);
    Console.WriteLine("Signing in with Google...");
    Console.WriteLine("A browser window will open. Please sign in and grant YouTube access.\n");
    try
    {
        var logger = sp.GetRequiredService<ILogger<YouTubeApiService>>();
        var service = await YouTubeApiService.CreateWithEmbeddedOAuthAsync("default", logger);
        var playlists = await service.GetUserPlaylistsAsync();
        Console.WriteLine($"Login successful! Found {playlists.Count} playlists on your account.");
        Console.WriteLine($"Credentials saved to {AppSettings.CredentialsPath}");
        service.Dispose();
    }
    catch (Exception ex) { Console.Error.WriteLine($"Login failed: {ex.Message}"); }
}

void RunLogout()
{
    var tokenDir = Path.Combine(AppSettings.OAuthTokenDir, "default");
    if (Directory.Exists(tokenDir))
    {
        Directory.Delete(tokenDir, true);
        Console.WriteLine("Logged out. OAuth tokens removed.");
    }
    else Console.WriteLine("Not logged in.");
}
