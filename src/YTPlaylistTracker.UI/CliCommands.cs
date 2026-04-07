using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using YTPlaylistTracker.Application.Services;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Infrastructure.Configuration;
using YTPlaylistTracker.Infrastructure.Platform;
using YTPlaylistTracker.Infrastructure.Update;
using YTPlaylistTracker.Infrastructure.YouTube;
using YTPlaylistTracker.UI.Views;
using App = Terminal.Gui.Application;

namespace YTPlaylistTracker.UI;

internal static class CliCommands
{
    internal static async Task RunUi(ServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var s = scope.ServiceProvider;
        App.Init();
        Theme.Apply();
        try
        {
            var mainWindow = new MainWindow(
                s.GetRequiredService<IPlaylistRepository>(),
                s.GetRequiredService<IProfileRepository>(),
                s.GetRequiredService<ISyncService>(),
                s.GetRequiredService<IYouTubeApiService>(),
                sp.GetRequiredService<IBrowserLauncher>(),
                sp.GetRequiredService<IUserSettings>(),
                sp.GetRequiredService<IUpdateService>(),
                s.GetRequiredService<ILogger<MainWindow>>());
            await mainWindow.InitializeAsync();
            App.Run(mainWindow);
            mainWindow.Dispose();
        }
        finally
        {
            App.Shutdown();
        }
    }

    internal static async Task RunSync(ServiceProvider sp, string? playlistId)
    {
        using var scope = sp.CreateScope();
        var s = scope.ServiceProvider;
        var syncService = s.GetRequiredService<ISyncService>();
        var playlistRepo = s.GetRequiredService<IPlaylistRepository>();
        var profileRepo = s.GetRequiredService<IProfileRepository>();

        var profile = await profileRepo.GetDefaultAsync();
        if (profile is null) { Console.Error.WriteLine("No profile configured. Run 'ytpt ui' first."); return; }

        // Backfill channel info if not yet populated
        if (profile.ChannelTitle is null && YouTubeApiService.HasStoredToken("default"))
        {
            try
            {
                var ytService = s.GetRequiredService<IYouTubeApiService>();
                var channel = await ytService.GetMyChannelAsync();
                if (channel is not null)
                {
                    profile.YouTubeChannelId = channel.ChannelId;
                    profile.ChannelTitle = channel.Title;
                    profile.ChannelThumbnailUrl = channel.ThumbnailUrl;
                    await profileRepo.UpdateAsync(profile);
                }
            }
            catch (Exception ex) { Log.Warning(ex, "Failed to fetch channel info during sync"); }
        }

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

    internal static async Task RunStatus(ServiceProvider sp)
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

    internal static async Task RunLogin(ServiceProvider sp)
    {
        if (string.IsNullOrWhiteSpace(AppSettings.OAuthClientId) || string.IsNullOrWhiteSpace(AppSettings.OAuthClientSecret))
        {
            Console.Error.WriteLine("OAuth client ID/secret not configured.");
            Console.Error.WriteLine("Set YTPT_CLIENT_ID and YTPT_CLIENT_SECRET env vars first.");
            return;
        }
        AppSettings.SaveCredentials(AppSettings.OAuthClientId, AppSettings.OAuthClientSecret);
        Console.WriteLine("Signing in with Google...");
        Console.WriteLine("A browser window will open. Please sign in and grant YouTube access.");
        Console.WriteLine("If the browser doesn't open automatically, check the console output below for the authorization URL.");
        Console.WriteLine();
        try
        {
            var logger = sp.GetRequiredService<ILogger<YouTubeApiService>>();
            var service = await YouTubeApiService.CreateWithEmbeddedOAuthAsync("default", logger);
            var playlists = await service.GetUserPlaylistsAsync();
            Console.WriteLine($"Login successful! Found {playlists.Count} playlists on your account.");
            Console.WriteLine($"Credentials saved to {AppSettings.CredentialsPath}");

            // Enrich profile with channel info
            using var scope = sp.CreateScope();
            var profileRepo = scope.ServiceProvider.GetRequiredService<IProfileRepository>();
            var profile = await profileRepo.GetDefaultAsync();
            if (profile is not null)
            {
                var channel = await service.GetMyChannelAsync();
                if (channel is not null)
                {
                    profile.YouTubeChannelId = channel.ChannelId;
                    profile.ChannelTitle = channel.Title;
                    profile.ChannelThumbnailUrl = channel.ThumbnailUrl;
                    await profileRepo.UpdateAsync(profile);
                    Console.WriteLine($"Profile updated: {channel.Title}");
                }
            }

            service.Dispose();
        }
        catch (Exception ex) { Console.Error.WriteLine($"Login failed: {ex.Message}"); }
    }

    internal static async Task RunExport(ServiceProvider sp, string format, string? outputPath)
    {
        using var scope = sp.CreateScope();
        var s = scope.ServiceProvider;
        var profileRepo = s.GetRequiredService<IProfileRepository>();
        var playlistRepo = s.GetRequiredService<IPlaylistRepository>();

        var profile = await profileRepo.GetDefaultAsync();
        if (profile is null) { Console.Error.WriteLine("No profile configured. Run 'ytpt ui' first."); return; }

        var removedVideos = await playlistRepo.GetAllDeletedVideosAsync(profile.Id);
        if (removedVideos.Count == 0) { Console.WriteLine("No removed videos found."); return; }

        var entries = ExportService.BuildEntries(removedVideos);
        var content = format.ToLower() switch
        {
            "json" => ExportService.ToJson(entries),
            _ => ExportService.ToCsv(entries)
        };

        if (outputPath is not null)
        {
            await File.WriteAllTextAsync(outputPath, content);
            Console.WriteLine($"Exported {entries.Count} removed videos to {outputPath}");
        }
        else
        {
            Console.Write(content);
        }
    }

    internal static void RunLogout()
    {
        var tokenDir = Path.Combine(AppSettings.OAuthTokenDir, "default");
        if (Directory.Exists(tokenDir))
        {
            Directory.Delete(tokenDir, true);
            Console.WriteLine("Logged out. OAuth tokens removed.");
        }
        else Console.WriteLine("Not logged in.");
    }

    internal static async Task RunUpdate(ServiceProvider sp)
    {
        var updateService = sp.GetRequiredService<IUpdateService>();
        var currentVersion = UpdateService.GetCurrentVersion();
        Console.WriteLine($"Current version: {currentVersion}");
        Console.WriteLine("Checking for updates...");

        var update = await updateService.CheckForUpdateAsync();
        if (!update.IsUpdateAvailable)
        {
            Console.WriteLine("You're on the latest version.");
            return;
        }

        Console.WriteLine($"New version available: {update.LatestVersion}");
        try
        {
            var message = await updateService.ApplyUpdateAsync(update);
            Console.WriteLine(message);
        }
        catch (UpdateException ex)
        {
            Console.Error.WriteLine($"Update failed: {ex.Message}");
            if (ex.ManualDownloadUrl is not null)
                Console.Error.WriteLine($"Download manually: {ex.ManualDownloadUrl}");
        }
    }
}
