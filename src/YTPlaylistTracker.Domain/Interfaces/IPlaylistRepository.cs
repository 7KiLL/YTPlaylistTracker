using YTPlaylistTracker.Domain.Entities;

namespace YTPlaylistTracker.Domain.Interfaces;

public interface IPlaylistRepository
{
    Task<List<Playlist>> GetByProfileAsync(int profileId);
    Task<List<Playlist>> GetTrackedByProfileAsync(int profileId);
    Task<Playlist?> GetByIdAsync(int id);
    Task AddAsync(Playlist playlist);
    Task UpdateAsync(Playlist playlist);
    Task DeleteAsync(int id);

    Task<List<Video>> GetVideosAsync(int playlistId);
    Task<List<Video>> GetActiveVideosAsync(int playlistId);
    Task<List<Video>> GetDeletedVideosAsync(int playlistId);
    Task AddVideoAsync(Video video);
    Task AddVideosAsync(IEnumerable<Video> videos);
    Task UpdateVideoAsync(Video video);
    Task PurgeDeletedVideosAsync(int playlistId);
    Task<List<(Playlist Playlist, Video Video)>> GetAllDeletedVideosAsync(int profileId);
}
