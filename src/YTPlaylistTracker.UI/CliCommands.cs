using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using YTPlaylistTracker.Application.Services;
using YTPlaylistTracker.Domain.Entities;
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
        var scope = sp.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var s = scope.ServiceProvider;
            App.Init();
            Theme.Apply(sp.GetRequiredService<IUserSettings>().ThemeName);
            try
            {
                var mainWindow = new MainWindow(
                    s.GetRequiredService<IPlaylistRepository>(),
                    s.GetRequiredService<IProfileRepository>(),
                    s.GetRequiredService<ISyncService>(),
                    sp.GetRequiredService<IYouTubeApiServiceFactory>(),
                    sp.GetRequiredService<ISystemLauncher>(),
                    sp.GetRequiredService<IUserSettings>(),
                    sp.GetRequiredService<IUpdateService>(),
                    s.GetRequiredService<ILogger<MainWindow>>());
                await mainWindow.InitializeAsync().ConfigureAwait(false);
                App.Run(mainWindow);
                mainWindow.Dispose();
            }
            finally
            {
                App.Shutdown();
            }
        }
    }

    internal static async Task RunSync(ServiceProvider sp, string? playlistId, string? profileName = null)
    {
        var scope = sp.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var s = scope.ServiceProvider;
            var syncService = s.GetRequiredService<ISyncService>();
            var playlistRepo = s.GetRequiredService<IPlaylistRepository>();
            var profileRepo = s.GetRequiredService<IProfileRepository>();
            var factory = sp.GetRequiredService<IYouTubeApiServiceFactory>();

            var profile = await ResolveProfileAsync(profileRepo, profileName).ConfigureAwait(false);
            if (profile is null) return;

            var youtube = await factory.CreateForProfileAsync(profile).ConfigureAwait(false);

            // Backfill channel info if not yet populated
            if (profile.ChannelTitle is null && factory.IsAuthenticated(profile))
            {
                try
                {
                    var channel = await youtube.GetMyChannelAsync().ConfigureAwait(false);
                    if (channel is not null)
                    {
                        profile.YouTubeChannelId = channel.ChannelId;
                        profile.ChannelTitle = channel.Title;
                        profile.ChannelThumbnailUrl = channel.ThumbnailUrl;
                        await profileRepo.UpdateAsync(profile).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) { Log.Warning(ex, "Failed to fetch channel info during sync"); }
            }

            if (playlistId is not null)
            {
                var playlists = await playlistRepo.GetByProfileAsync(profile.Id).ConfigureAwait(false);
                var playlist = playlists.FirstOrDefault(p => string.Equals(p.YouTubePlaylistId, playlistId, StringComparison.Ordinal));
                if (playlist is null) { Console.Error.WriteLine($"Playlist {playlistId} not found."); return; }
                var result = await syncService.SyncPlaylistAsync(playlist, youtube).ConfigureAwait(false);
                Console.WriteLine($"Synced: +{result.Added} added, -{result.Removed} removed, ~{result.Updated} updated");
            }
            else
            {
                var results = await syncService.SyncAllTrackedAsync(profile.Id, youtube).ConfigureAwait(false);
                foreach (var (id, result) in results)
                    Console.WriteLine($"Playlist {id}: +{result.Added} -{result.Removed} ~{result.Updated}");
                Console.WriteLine($"Total: {results.Count} playlists synced");
            }

            if (youtube is IDisposable d) d.Dispose();
        }
    }
    internal static async Task RunStatus(ServiceProvider sp)
    {
        var scope = sp.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var s = scope.ServiceProvider;
            var profileRepo = s.GetRequiredService<IProfileRepository>();
            var playlistRepo = s.GetRequiredService<IPlaylistRepository>();
            var factory = sp.GetRequiredService<IYouTubeApiServiceFactory>();

            var profiles = await profileRepo.GetAllAsync().ConfigureAwait(false);
            foreach (var profile in profiles)
            {
                var authStatus = profile.IsOffline ? "offline" : factory.IsAuthenticated(profile) ? "logged in" : "not logged in";
                Console.WriteLine($"Profile: {profile.Name}{(profile.IsDefault ? " (default)" : "")} [{authStatus}]");
                var playlists = await playlistRepo.GetByProfileAsync(profile.Id).ConfigureAwait(false);
                foreach (var pl in playlists)
                {
                    var videos = await playlistRepo.GetVideosAsync(pl.Id).ConfigureAwait(false);
                    var active = videos.Count(v => v.DeletedAt == null);
                    var deleted = videos.Count(v => v.DeletedAt != null);
                    Console.WriteLine($"  {(pl.IsTracked ? "✓" : " ")} {pl.Title ?? pl.YouTubePlaylistId} — {active} active, {deleted} removed, last sync: {pl.LastSyncedAt?.ToString("g") ?? "never"}");
                }
            }
        }
    }

    internal static async Task RunLogin(ServiceProvider sp, string? profileName = null)
    {
        if (string.IsNullOrWhiteSpace(AppSettings.OAuthClientId) || string.IsNullOrWhiteSpace(AppSettings.OAuthClientSecret))
        {
            Console.Error.WriteLine("OAuth client ID/secret not configured.");
            Console.Error.WriteLine("Set YTPT_CLIENT_ID and YTPT_CLIENT_SECRET env vars first.");
            return;
        }
        AppSettings.SaveCredentials(AppSettings.OAuthClientId, AppSettings.OAuthClientSecret);

        var scope = sp.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var profileRepo = scope.ServiceProvider.GetRequiredService<IProfileRepository>();

            // Resolve or create profile
            Profile? profile = null;
            if (profileName is not null)
            {
                profile = await profileRepo.GetByNameAsync(profileName).ConfigureAwait(false);
                if (profile is null)
                {
                    profile = new Profile { Name = profileName, IsDefault = false };
                    await profileRepo.AddAsync(profile).ConfigureAwait(false);
                    Console.WriteLine($"Created profile: {profileName}");
                }
            }
            else
            {
                profile = await profileRepo.GetDefaultAsync().ConfigureAwait(false);
                if (profile is null)
                {
                    profile = new Profile { Name = "Default", IsDefault = true };
                    await profileRepo.AddAsync(profile).ConfigureAwait(false);
                }
            }

            var slug = YouTubeApiServiceFactory.ToProfileSlug(profile.Name);
            Console.WriteLine($"Signing in for profile: {profile.Name} (token: {slug})");
            Console.WriteLine("A browser window will open. Please sign in and grant YouTube access.");
            Console.WriteLine("If the browser doesn't open automatically, check the console output below for the authorization URL.");
            Console.WriteLine();

            try
            {
                var logger = sp.GetRequiredService<ILogger<YouTubeApiService>>();
                var service = await YouTubeApiService.CreateWithEmbeddedOAuthAsync(slug, logger).ConfigureAwait(false);
                var playlists = await service.GetUserPlaylistsAsync().ConfigureAwait(false);
                Console.WriteLine($"Login successful! Found {playlists.Count} playlists on your account.");

                var channel = await service.GetMyChannelAsync().ConfigureAwait(false);
                if (channel is not null)
                {
                    profile.YouTubeChannelId = channel.ChannelId;
                    profile.ChannelTitle = channel.Title;
                    profile.ChannelThumbnailUrl = channel.ThumbnailUrl;
                    await profileRepo.UpdateAsync(profile).ConfigureAwait(false);
                    Console.WriteLine($"Profile updated: {channel.Title}");
                }

                service.Dispose();
            }
            catch (Exception ex) { Console.Error.WriteLine($"Login failed: {ex.Message}"); }
        }
    }

    internal static void RunLogout(ServiceProvider sp, string? profileName = null)
    {
        var scope = sp.CreateScope();
        var profileRepo = scope.ServiceProvider.GetRequiredService<IProfileRepository>();

        string slug;
        if (profileName is not null)
        {
            slug = YouTubeApiServiceFactory.ToProfileSlug(profileName);
        }
        else
        {
            var profile = profileRepo.GetDefaultAsync().GetAwaiter().GetResult();
            slug = profile is not null ? YouTubeApiServiceFactory.ToProfileSlug(profile.Name) : "default";
        }

        var tokenDir = Path.Combine(AppSettings.OAuthTokenDir, slug);
        if (Directory.Exists(tokenDir))
        {
            Directory.Delete(tokenDir, recursive: true);
            Console.WriteLine($"Logged out ({slug}). OAuth tokens removed.");
        }
        else Console.WriteLine("Not logged in.");
    }

    internal static async Task RunExport(ServiceProvider sp, string format, string? outputPath, string? profileName = null)
    {
        var scope = sp.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var s = scope.ServiceProvider;
            var profileRepo = s.GetRequiredService<IProfileRepository>();
            var playlistRepo = s.GetRequiredService<IPlaylistRepository>();

            var profile = await ResolveProfileAsync(profileRepo, profileName).ConfigureAwait(false);
            if (profile is null) return;

            var removedVideos = await playlistRepo.GetAllDeletedVideosAsync(profile.Id).ConfigureAwait(false);
            if (removedVideos.Count == 0) { Console.WriteLine("No removed videos found."); return; }

            var entries = ExportService.BuildEntries(removedVideos);
            var content = format.ToLowerInvariant() switch
            {
                "json" => ExportService.ToJson(entries),
                _ => ExportService.ToCsv(entries),
            };

            if (outputPath is not null)
            {
                await File.WriteAllTextAsync(outputPath, content).ConfigureAwait(false);
                Console.WriteLine($"Exported {entries.Count} removed videos to {outputPath}");
            }
            else
            {
                Console.Write(content);
            }
        }
    }
    internal static async Task RunUpdate(ServiceProvider sp)
    {
        var updateService = sp.GetRequiredService<IUpdateService>();
        var currentVersion = UpdateService.GetCurrentVersion();
        Console.WriteLine($"Current version: {currentVersion}");
        Console.WriteLine("Checking for updates...");

        var update = await updateService.CheckForUpdateAsync().ConfigureAwait(false);
        if (!update.IsUpdateAvailable)
        {
            Console.WriteLine("You're on the latest version.");
            return;
        }

        Console.WriteLine($"New version available: {update.LatestVersion}");
        try
        {
            var message = await updateService.ApplyUpdateAsync(update).ConfigureAwait(false);
            Console.WriteLine(message);
        }
        catch (UpdateException ex)
        {
            Console.Error.WriteLine($"Update failed: {ex.Message}");
            if (ex.ManualDownloadUrl is not null)
                Console.Error.WriteLine($"Download manually: {ex.ManualDownloadUrl}");
        }
    }

    private static async Task<Profile?> ResolveProfileAsync(IProfileRepository profileRepo, string? profileName)
    {
        if (profileName is not null)
        {
            var profile = await profileRepo.GetByNameAsync(profileName).ConfigureAwait(false);
            if (profile is null)
            {
                Console.Error.WriteLine($"Profile '{profileName}' not found.");
                return null;
            }
            return profile;
        }

        var defaultProfile = await profileRepo.GetDefaultAsync().ConfigureAwait(false);
        if (defaultProfile is null)
        {
            Console.Error.WriteLine("No profile configured. Run 'ytpt ui' first.");
            return null;
        }
        return defaultProfile;
    }
}
