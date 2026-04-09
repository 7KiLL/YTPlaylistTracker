using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using YTPlaylistTracker.Application.Services;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Infrastructure.Configuration;
using YTPlaylistTracker.Infrastructure.Data;
using YTPlaylistTracker.Infrastructure.Platform;
using YTPlaylistTracker.Infrastructure.Update;
using YTPlaylistTracker.Infrastructure.YouTube;
using YTPlaylistTracker.UI;
using CliCommand = System.CommandLine.Command;
using CliRootCommand = System.CommandLine.RootCommand;
using System.CommandLine;

// Bootstrap
AppSettings.OAuthClientId = BuildConstants.OAuthClientId;
AppSettings.OAuthClientSecret = BuildConstants.OAuthClientSecret;
AppSettings.EnsureDirectories();
AppSettings.LoadCredentials();
var userSettingsBootstrap = UserSettings.Load();
AppSettings.LoadApiKey(userSettingsBootstrap.YouTubeApiKey);
GlyphDetector.SetUserOverride(userSettingsBootstrap.GlyphMode);
AppSettings.YouTubeApiKey = string.IsNullOrWhiteSpace(AppSettings.YouTubeApiKey) ? BuildConstants.YouTubeApiKey : AppSettings.YouTubeApiKey;

var isVerbose = args.Contains("--verbose", StringComparer.Ordinal) || args.Contains("-v", StringComparer.Ordinal);
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(isVerbose ? Serilog.Events.LogEventLevel.Debug : Serilog.Events.LogEventLevel.Information)
    .WriteTo.File(
        Path.Combine(AppSettings.LogDir, "ytpt-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

// Global exception handlers — catch crashes that bypass all other try-catch
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    Log.Fatal(e.ExceptionObject as Exception, "UNHANDLED EXCEPTION (AppDomain)");
    Log.CloseAndFlush();
};
TaskScheduler.UnobservedTaskException += (_, e) =>
{
    Log.Error(e.Exception, "UNOBSERVED TASK EXCEPTION");
    e.SetObserved();
};

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
services.AddSingleton<ISystemLauncher, SystemLauncher>();
services.AddSingleton<IUserSettings>(UserSettings.Load());
services.AddSingleton<IBinaryUpdater>(sp =>
    OperatingSystem.IsWindows()
        ? new WindowsBinaryUpdater(sp.GetRequiredService<ILogger<WindowsBinaryUpdater>>())
        : new UnixBinaryUpdater(sp.GetRequiredService<ILogger<UnixBinaryUpdater>>()));
services.AddSingleton<IUpdateService>(sp =>
    new UpdateService(
        sp.GetRequiredService<IBinaryUpdater>(),
        sp.GetRequiredService<ILogger<UpdateService>>()));
services.AddSingleton<IYouTubeApiServiceFactory, YouTubeApiServiceFactory>();

sp = services.BuildServiceProvider();

var scope = sp.CreateAsyncScope();
await using (scope.ConfigureAwait(false))
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Handle upgrade from v0.1.0 (EnsureCreated) to migrations
    var conn = db.Database.GetDbConnection();
    await conn.OpenAsync().ConfigureAwait(false);
    var cmd = conn.CreateCommand();
    await using (cmd.ConfigureAwait(false))
    {
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory'";
        var hasMigrationHistory = await cmd.ExecuteScalarAsync().ConfigureAwait(false) != null;

        if (!hasMigrationHistory)
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Profiles'";
            var hasExistingTables = await cmd.ExecuteScalarAsync().ConfigureAwait(false) != null;

            if (hasExistingTables)
            {
                // Legacy DB from v0.1.0 — stamp InitialCreate as already applied
                var pendingMigrations = await db.Database.GetPendingMigrationsAsync().ConfigureAwait(false);
                var initialMigration = pendingMigrations.FirstOrDefault(m => m.EndsWith("_InitialCreate", StringComparison.Ordinal));
                if (initialMigration != null)
                {
                    await db.Database.ExecuteSqlRawAsync(
                        "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" TEXT NOT NULL, \"ProductVersion\" TEXT NOT NULL, PRIMARY KEY(\"MigrationId\"))").ConfigureAwait(false);

                    var efVersion = typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "10.0.5";
                    await db.Database.ExecuteSqlRawAsync(
                        "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({0}, {1})",
                        initialMigration, efVersion).ConfigureAwait(false);
                    Log.Information("Upgraded legacy database: stamped {Migration}", initialMigration);
                }
            }
        }
    }

    await db.Database.MigrateAsync().ConfigureAwait(false);
    AppSettings.SecureDbFile();
}

// --- CLI ---
var root = new CliRootCommand("ytpt — YouTube Playlist Tracker");

var verboseOpt = new System.CommandLine.Option<bool>("--verbose", "-v") { Description = "Enable debug logging" };
root.Add(verboseOpt);

var profileOpt = new System.CommandLine.Option<string?>("--profile", "-p") { Description = "Profile name (default: default profile)" };

// ui
var uiCmd = new CliCommand("ui", "Launch the interactive TUI");
uiCmd.SetAction(async (_, _) => await CliCommands.RunUi(sp).ConfigureAwait(false));
root.Add(uiCmd);

// sync [playlist-id] [--profile]
var syncArg = new System.CommandLine.Argument<string?>("playlist-id") { Description = "Playlist ID. Omit to sync all.", Arity = ArgumentArity.ZeroOrOne };
var syncCmd = new CliCommand("sync", "Sync playlists from YouTube") { syncArg, profileOpt };
syncCmd.SetAction(async (result, _) =>
{
    var playlistId = result.GetValue(syncArg);
    var profileName = result.GetValue(profileOpt);
    await CliCommands.RunSync(sp, playlistId, profileName).ConfigureAwait(false);
});
root.Add(syncCmd);

// status
var statusCmd = new CliCommand("status", "Show tracking summary");
statusCmd.SetAction(async (_, _) => await CliCommands.RunStatus(sp).ConfigureAwait(false));
root.Add(statusCmd);

// login [--profile]
var loginCmd = new CliCommand("login", "Sign in with Google (opens browser)") { profileOpt };
loginCmd.SetAction(async (result, _) =>
{
    var profileName = result.GetValue(profileOpt);
    await CliCommands.RunLogin(sp, profileName).ConfigureAwait(false);
});
root.Add(loginCmd);

// logout [--profile]
var logoutCmd = new CliCommand("logout", "Sign out and remove stored tokens") { profileOpt };
logoutCmd.SetAction((result, _) =>
{
    var profileName = result.GetValue(profileOpt);
    CliCommands.RunLogout(sp, profileName);
    return Task.CompletedTask;
});
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
        var key = Console.ReadLine()?.Trim().ToLowerInvariant();
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

// export [--format csv|json] [--output path]
var exportFormatOpt = new System.CommandLine.Option<string>("--format", "-f") { Description = "Output format: csv or json (default: csv)" };
var exportOutputOpt = new System.CommandLine.Option<string?>("--output", "-o") { Description = "Output file path (default: stdout)" };
var exportCmd = new CliCommand("export", "Export removed videos report") { exportFormatOpt, exportOutputOpt, profileOpt };
exportCmd.SetAction(async (result, _) =>
{
    var format = result.GetValue(exportFormatOpt);
    if (string.IsNullOrEmpty(format)) format = "csv";
    var output = result.GetValue(exportOutputOpt);
    var profileName = result.GetValue(profileOpt);
    await CliCommands.RunExport(sp, format, output, profileName).ConfigureAwait(false);
});
root.Add(exportCmd);

// update
var updateCmd = new CliCommand("update", "Check for and apply updates");
updateCmd.SetAction(async (_, _) => await CliCommands.RunUpdate(sp).ConfigureAwait(false));
root.Add(updateCmd);

// Default: launch TUI
root.SetAction(async (_, _) => await CliCommands.RunUi(sp).ConfigureAwait(false));

return await root.Parse(args).InvokeAsync().ConfigureAwait(false);
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
    await Log.CloseAndFlushAsync().ConfigureAwait(false);
}
