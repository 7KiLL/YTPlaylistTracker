using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Domain.Enums;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Domain.Models;
using YTPlaylistTracker.Infrastructure.Configuration;

namespace YTPlaylistTracker.Infrastructure.YouTube;

public class YouTubeApiService : IYouTubeApiService, IDisposable
{
    private readonly YouTubeService _youtube;
    private readonly ILogger<YouTubeApiService> _logger;

    private YouTubeApiService(YouTubeService youtube, ILogger<YouTubeApiService> logger)
    {
        _youtube = youtube;
        _logger = logger;
    }

    public static async Task<YouTubeApiService> CreateWithOAuthAsync(
        string clientSecretsPath,
        string profileName,
        ILogger<YouTubeApiService> logger)
    {
        logger.LogInformation("Authenticating with OAuth2 (file) for profile: {Profile}", profileName);

        var tokenDir = Path.Combine(AppSettings.OAuthTokenDir, profileName);
        var stream = new FileStream(clientSecretsPath, FileMode.Open, FileAccess.Read);
        await using (stream.ConfigureAwait(false))
        {
            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
(await GoogleClientSecrets.FromStreamAsync(stream)).Secrets,
            [YouTubeService.Scope.YoutubeReadonly],
            "user",
            CancellationToken.None,
            new FileDataStore(tokenDir, fullPath: true)).ConfigureAwait(false);

        var youtube = new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "ytpt",
        });

        logger.LogInformation("OAuth2 authentication successful");
        return new YouTubeApiService(youtube, logger);
        }
    }

    /// <summary>
    /// Create with bundled OAuth client ID/secret (no client_secrets.json file needed).
    /// Users just see "Sign in with Google" in their browser.
    /// </summary>
    public static async Task<YouTubeApiService> CreateWithEmbeddedOAuthAsync(
        string profileName,
        ILogger<YouTubeApiService> logger)
    {
        logger.LogInformation("Authenticating with OAuth2 (embedded) for profile: {Profile}", profileName);

        var clientId = Environment.GetEnvironmentVariable("YTPT_CLIENT_ID")
                       ?? AppSettings.OAuthClientId;
        var clientSecret = Environment.GetEnvironmentVariable("YTPT_CLIENT_SECRET")
                           ?? AppSettings.OAuthClientSecret;

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException(
                "OAuth client ID/secret not configured. Set YTPT_CLIENT_ID and YTPT_CLIENT_SECRET env vars, " +
                "or place client_secrets.json in " + AppSettings.AppDataDir);

        var tokenDir = Path.Combine(AppSettings.OAuthTokenDir, profileName);
        var secrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret };

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            [YouTubeService.Scope.YoutubeReadonly],
            "user",
            CancellationToken.None,
            new FileDataStore(tokenDir, fullPath: true)).ConfigureAwait(false);

        var youtube = new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "ytpt",
        });

        logger.LogInformation("OAuth2 authentication successful");
        return new YouTubeApiService(youtube, logger);
    }

    /// <summary>
    /// Check if a valid OAuth token already exists for the given profile.
    /// </summary>
    public static bool HasStoredToken(string profileName)
    {
        var tokenDir = Path.Combine(AppSettings.OAuthTokenDir, profileName);
        var tokenFile = Path.Combine(tokenDir, "Google.Apis.Auth.OAuth2.Responses.TokenResponse-user");
        return File.Exists(tokenFile);
    }

    public static YouTubeApiService CreateWithApiKey(string apiKey, ILogger<YouTubeApiService> logger)
    {
        logger.LogInformation("Authenticating with API key");
        var youtube = new YouTubeService(new BaseClientService.Initializer
        {
            ApiKey = apiKey,
            ApplicationName = "ytpt",
        });
        return new YouTubeApiService(youtube, logger);
    }

    public async Task<IReadOnlyList<YouTubeVideoSnapshot>> GetPlaylistVideosAsync(string playlistId)
    {
        _logger.LogInformation("[API] GET playlistItems.list playlist={PlaylistId}", playlistId);
        var results = new List<YouTubeVideoSnapshot>();
        string? nextPageToken = null;

        do
        {
            var request = _youtube.PlaylistItems.List("snippet,contentDetails");
            request.PlaylistId = playlistId;
            request.MaxResults = 50;
            request.PageToken = nextPageToken;

            var response = await request.ExecuteAsync().ConfigureAwait(false);
            _logger.LogInformation("[API] <- {Count} items, nextPage={Next}",
                response.Items.Count, response.NextPageToken ?? "(none)");

            foreach (var item in response.Items)
            {
                if (item.Snippet.Title is "Deleted video" or "Private video")
                {
                    _logger.LogDebug("Skipping unavailable video: {VideoId}", item.ContentDetails.VideoId);
                    continue;
                }

                DateTime? addedAt = null;
                try { addedAt = item.Snippet.PublishedAtDateTimeOffset?.UtcDateTime; }
                catch { /* PublishedAt can fail to parse on some items */ }

                string? json = null;
                try { json = Newtonsoft.Json.JsonConvert.SerializeObject(item.Snippet); }
                catch { /* best effort */ }

                var description = item.Snippet.Description;
                var thumbnailUrl = ExtractThumbnailUrl(item.Snippet.Thumbnails);

                results.Add(new YouTubeVideoSnapshot(
                    item.ContentDetails.VideoId,
                    item.Snippet.Title,
                    item.Snippet.VideoOwnerChannelTitle,
                    (int)(item.Snippet.Position ?? 0),
                    addedAt,
                    description,
                    thumbnailUrl,
                    json));
            }

            nextPageToken = response.NextPageToken;
        } while (nextPageToken is not null);

        _logger.LogInformation("Fetched {Count} videos from playlist {PlaylistId}", results.Count, playlistId);
        return results;
    }

    public async Task<YouTubePlaylistSnapshot?> GetPlaylistMetadataAsync(string playlistId)
    {
        _logger.LogInformation("[API] GET playlists.list id={PlaylistId}", playlistId);
        var request = _youtube.Playlists.List("snippet");
        request.Id = playlistId;

        var response = await request.ExecuteAsync().ConfigureAwait(false);
        var item = response.Items.FirstOrDefault();
        if (item is null)
        {
            _logger.LogWarning("Playlist not found: {PlaylistId}", playlistId);
            return null;
        }

        string? json = null;
        try { json = Newtonsoft.Json.JsonConvert.SerializeObject(item.Snippet); }
        catch { /* best effort */ }

        DateTime? publishedAt = null;
        try { publishedAt = item.Snippet.PublishedAtDateTimeOffset?.UtcDateTime; }
        catch { /* best effort */ }

        var description = item.Snippet.Description;
        var thumbnailUrl = ExtractThumbnailUrl(item.Snippet.Thumbnails);

        return new YouTubePlaylistSnapshot(item.Id, item.Snippet.Title, item.Snippet.ChannelTitle,
            description, thumbnailUrl, publishedAt, json);
    }

    public async Task<IReadOnlyList<YouTubePlaylistSnapshot>> GetUserPlaylistsAsync()
    {
        _logger.LogInformation("Fetching user's own playlists");
        var results = new List<YouTubePlaylistSnapshot>();
        string? nextPageToken = null;

        do
        {
            var request = _youtube.Playlists.List("snippet");
            request.Mine = true;
            request.MaxResults = 50;
            request.PageToken = nextPageToken;

            var response = await request.ExecuteAsync().ConfigureAwait(false);
            foreach (var item in response.Items)
            {
                string? json = null;
                try { json = Newtonsoft.Json.JsonConvert.SerializeObject(item.Snippet); }
                catch { /* best effort */ }

                DateTime? publishedAt = null;
                try { publishedAt = item.Snippet.PublishedAtDateTimeOffset?.UtcDateTime; }
                catch { /* best effort */ }

                var description = item.Snippet.Description;
                var thumbnailUrl = ExtractThumbnailUrl(item.Snippet.Thumbnails);

                results.Add(new YouTubePlaylistSnapshot(item.Id, item.Snippet.Title, item.Snippet.ChannelTitle,
                    description, thumbnailUrl, publishedAt, json));
            }

            nextPageToken = response.NextPageToken;
        } while (nextPageToken is not null);

        _logger.LogInformation("Found {Count} user playlists", results.Count);
        return results;
    }

    public async Task<YouTubeChannelSnapshot?> GetMyChannelAsync()
    {
        _logger.LogInformation("[API] GET channels.list mine=true");
        var request = _youtube.Channels.List("snippet,contentDetails");
        request.Mine = true;

        var response = await request.ExecuteAsync().ConfigureAwait(false);
        var item = response.Items.FirstOrDefault();
        if (item is null)
        {
            _logger.LogWarning("[API] No channel found for authenticated user");
            return null;
        }

        var thumbnailUrl = ExtractThumbnailUrl(item.Snippet.Thumbnails);
        var likedPlaylistId = item.ContentDetails?.RelatedPlaylists?.Likes;

        _logger.LogInformation("[API] Channel: {Title} ({Id}), LikedPlaylist: {LikedId}",
            item.Snippet.Title, item.Id, likedPlaylistId ?? "(none)");
        return new YouTubeChannelSnapshot(item.Id, item.Snippet.Title, thumbnailUrl, likedPlaylistId);
    }

    public async Task<RemovalReason> CheckVideoStatusAsync(string videoId)
    {
        _logger.LogInformation("[API] GET videos.list id={VideoId}", videoId);
        try
        {
            var request = _youtube.Videos.List("status");
            request.Id = videoId;
            var response = await request.ExecuteAsync().ConfigureAwait(false);

            if (response.Items.Count == 0)
                return RemovalReason.Deleted;

            return response.Items[0].Status.PrivacyStatus switch
            {
                "private" => RemovalReason.Private,
                "unlisted" => RemovalReason.Unlisted,
                _ => RemovalReason.RemovedByOwner,
            };
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return RemovalReason.Deleted;
        }
    }

    private static string? ExtractThumbnailUrl(Google.Apis.YouTube.v3.Data.ThumbnailDetails? thumbnails) =>
        thumbnails?.Medium?.Url ?? thumbnails?.Default__?.Url;

    public void Dispose()
    {
        _youtube.Dispose();
        GC.SuppressFinalize(this);
    }
}
