using YTPlaylistTracker.Domain.Entities;

namespace YTPlaylistTracker.Domain.Interfaces;

public interface IPlaylistRepository
{
    Task<IReadOnlyList<Playlist>> GetByProfileAsync(int profileId, CancellationToken ct = default);
    Task<IReadOnlyList<Playlist>> GetTrackedByProfileAsync(int profileId, CancellationToken ct = default);
    Task AddAsync(Playlist playlist, CancellationToken ct = default);
    Task AddPlaylistsAsync(IEnumerable<Playlist> playlists, CancellationToken ct = default);
    Task UpdateAsync(Playlist playlist, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Video>> GetVideosAsync(int playlistId, CancellationToken ct = default);
    Task<IReadOnlyList<Video>> GetDeletedVideosAsync(int playlistId, CancellationToken ct = default);
    Task AddVideosAsync(IEnumerable<Video> videos, CancellationToken ct = default);
    Task UpdateVideoAsync(Video video, CancellationToken ct = default);
    Task PurgeDeletedVideosAsync(int playlistId, CancellationToken ct = default);
    Task<IReadOnlyList<(Playlist Playlist, Video Video)>> GetAllDeletedVideosAsync(int profileId, CancellationToken ct = default);
}
