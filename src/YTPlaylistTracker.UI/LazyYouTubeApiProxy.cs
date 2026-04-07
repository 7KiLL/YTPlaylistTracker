using YTPlaylistTracker.Domain.Enums;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Domain.Models;

namespace YTPlaylistTracker.UI;

/// <summary>
/// Wraps Lazy&lt;IYouTubeApiService&gt; so the actual YouTube API client
/// is only created when a method is first called — not at DI resolution time.
/// This prevents OAuth token refresh from blocking startup for commands
/// that don't need the YouTube API (status, logout, --help).
/// </summary>
internal class LazyYouTubeApiProxy(Lazy<IYouTubeApiService> lazy) : IYouTubeApiService
{
    public Task<IReadOnlyList<YouTubeVideoSnapshot>> GetPlaylistVideosAsync(string playlistId)
        => lazy.Value.GetPlaylistVideosAsync(playlistId);

    public Task<YouTubePlaylistSnapshot?> GetPlaylistMetadataAsync(string playlistId)
        => lazy.Value.GetPlaylistMetadataAsync(playlistId);

    public Task<IReadOnlyList<YouTubePlaylistSnapshot>> GetUserPlaylistsAsync()
        => lazy.Value.GetUserPlaylistsAsync();

    public Task<RemovalReason> CheckVideoStatusAsync(string videoId)
        => lazy.Value.CheckVideoStatusAsync(videoId);

    public Task<YouTubeChannelSnapshot?> GetMyChannelAsync()
        => lazy.Value.GetMyChannelAsync();
}
