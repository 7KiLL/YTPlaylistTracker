using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Infrastructure.Configuration;

namespace YTPlaylistTracker.Infrastructure.YouTube;

public partial class YouTubeApiServiceFactory(
    ILogger<YouTubeApiServiceFactory> logger) : IYouTubeApiServiceFactory
{
    public async Task<IYouTubeApiService> CreateForProfileAsync(Profile profile)
    {
        var apiLogger = CreateApiLogger();

        if (profile.IsOffline)
        {
            var apiKey = ResolveApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
                logger.LogWarning("No YouTube API key configured. Offline profile will have limited functionality");

            return YouTubeApiService.CreateWithApiKey(apiKey, apiLogger);
        }

        var slug = ToProfileSlug(profile.Name);
        if (YouTubeApiService.HasStoredToken(slug))
        {
            logger.LogInformation("Creating OAuth service for profile: {Profile} (slug: {Slug})", profile.Name, slug);
            return await YouTubeApiService.CreateWithEmbeddedOAuthAsync(slug, apiLogger).ConfigureAwait(false);
        }

        // Not authenticated yet — fall back to API key for public playlist access
        logger.LogInformation("Profile {Profile} not authenticated, using API key fallback", profile.Name);
        var fallbackKey = ResolveApiKey();
        return YouTubeApiService.CreateWithApiKey(fallbackKey, apiLogger);
    }

    public async Task<IYouTubeApiService> LoginAsync(Profile profile, Action<string>? onAuthUrl = null)
    {
        var slug = ToProfileSlug(profile.Name);
        var tokenDir = Path.Combine(AppSettings.OAuthTokenDir, slug);

        // Clear any stale/corrupt tokens to force fresh consent
        if (Directory.Exists(tokenDir))
        {
            logger.LogInformation("Clearing existing tokens for profile: {Profile} (slug: {Slug})", profile.Name, slug);
            Directory.Delete(tokenDir, recursive: true);
        }

        logger.LogInformation("Starting OAuth login for profile: {Profile} (slug: {Slug})", profile.Name, slug);

        var clientId = Environment.GetEnvironmentVariable("YTPT_CLIENT_ID") ?? AppSettings.OAuthClientId;
        var clientSecret = Environment.GetEnvironmentVariable("YTPT_CLIENT_SECRET") ?? AppSettings.OAuthClientSecret;
        var secrets = new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret };

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = secrets,
            Scopes = [YouTubeService.Scope.YoutubeReadonly],
            DataStore = new FileDataStore(tokenDir, fullPath: true),
        });

        var codeReceiver = new LocalServerCodeReceiver();
        ICodeReceiver receiver = onAuthUrl is not null
            ? new UrlCapturingCodeReceiver(codeReceiver, onAuthUrl)
            : codeReceiver;

        var credential = await new AuthorizationCodeInstalledApp(flow, receiver)
            .AuthorizeAsync("user", CancellationToken.None).ConfigureAwait(false);

        var youtube = new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "ytpt",
        });

        logger.LogInformation("OAuth2 login successful for profile: {Profile}", profile.Name);
        return YouTubeApiService.CreateFromExisting(youtube, CreateApiLogger());
    }

    public bool IsAuthenticated(Profile profile)
    {
        if (profile.IsOffline) return false;
        var slug = ToProfileSlug(profile.Name);
        return YouTubeApiService.HasStoredToken(slug);
    }

    public static string ToProfileSlug(string profileName)
    {
        var slug = profileName.ToLowerInvariant().Trim();
        slug = InvalidCharsRegex().Replace(slug, "_");
        return string.IsNullOrWhiteSpace(slug) ? "default" : slug;
    }

    private static string ResolveApiKey()
    {
        // Priority: env var → AppSettings (already resolved from env/user-settings/build-constant)
        return AppSettings.YouTubeApiKey;
    }

    [GeneratedRegex(@"[^a-z0-9\-_]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex InvalidCharsRegex();

    /// <summary>
    /// Forwards log messages from a child logger factory to the parent logger.
    /// Needed because YouTubeApiService requires ILogger&lt;YouTubeApiService&gt; but we
    /// want all logging to flow through the factory's logger provider.
    /// </summary>
    private ILogger<YouTubeApiService> CreateApiLogger() =>
        LoggerFactory.Create(b => b.AddProvider(new ForwardingLoggerProvider(logger)))
            .CreateLogger<YouTubeApiService>();

    private sealed class ForwardingLoggerProvider(ILogger target) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => target;
        public void Dispose() { }
    }

    private sealed class UrlCapturingCodeReceiver(ICodeReceiver inner, Action<string> onUrl) : ICodeReceiver
    {
        public string RedirectUri => inner.RedirectUri;

        public Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(
            AuthorizationCodeRequestUrl url, CancellationToken taskCancellationToken)
        {
            onUrl(url.Build().AbsoluteUri);
            return inner.ReceiveCodeAsync(url, taskCancellationToken);
        }
    }
}
