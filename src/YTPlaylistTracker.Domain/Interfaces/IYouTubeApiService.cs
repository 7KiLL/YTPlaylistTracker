using YTPlaylistTracker.Domain.Enums;
using YTPlaylistTracker.Domain.Models;

namespace YTPlaylistTracker.Domain.Interfaces;

public interface IYouTubeApiService
{
    Task<List<YouTubeVideoSnapshot>> GetPlaylistVideosAsync(string playlistId);
    Task<YouTubePlaylistSnapshot?> GetPlaylistMetadataAsync(string playlistId);
    Task<List<YouTubePlaylistSnapshot>> GetUserPlaylistsAsync();
    Task<RemovalReason> CheckVideoStatusAsync(string videoId);
    Task<YouTubeChannelSnapshot?> GetMyChannelAsync();
}
