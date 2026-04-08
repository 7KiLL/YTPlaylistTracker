using YTPlaylistTracker.Domain.Entities;

namespace YTPlaylistTracker.Domain.Interfaces;

public interface ISyncService
{
    Task<SyncResult> SyncPlaylistAsync(Playlist playlist);
    Task<IReadOnlyDictionary<int, SyncResult>> SyncAllTrackedAsync(int profileId, IProgress<string>? progress = null);
}
