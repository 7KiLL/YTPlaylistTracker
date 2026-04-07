using YTPlaylistTracker.Domain.Entities;

namespace YTPlaylistTracker.Domain.Interfaces;

public record SyncResult(int Added, int Removed, int Updated);

public interface ISyncService
{
    Task<SyncResult> SyncPlaylistAsync(Playlist playlist);
    Task<Dictionary<int, SyncResult>> SyncAllTrackedAsync(int profileId, IProgress<string>? progress = null);
}
