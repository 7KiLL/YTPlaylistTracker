using YTPlaylistTracker.Domain.Enums;
using YTPlaylistTracker.Domain.Models;

namespace YTPlaylistTracker.Domain.Interfaces;

public interface IYouTubeApiService
{
    Task<IReadOnlyList<YouTubeVideoSnapshot>> GetPlaylistVideosAsync(string playlistId);
    Task<YouTubePlaylistSnapshot?> GetPlaylistMetadataAsync(string playlistId);
    Task<IReadOnlyList<YouTubePlaylistSnapshot>> GetUserPlaylistsAsync();
    Task<RemovalReason> CheckVideoStatusAsync(string videoId);
    Task<YouTubeChannelSnapshot?> GetMyChannelAsync();
}
