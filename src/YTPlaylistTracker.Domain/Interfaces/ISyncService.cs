using YTPlaylistTracker.Domain.Entities;

namespace YTPlaylistTracker.Domain.Interfaces;

public interface ISyncService
{
    Task<SyncResult> SyncPlaylistAsync(Playlist playlist, IYouTubeApiService youtube);
    Task<IReadOnlyDictionary<int, SyncResult>> SyncAllTrackedAsync(int profileId, IYouTubeApiService youtube, IProgress<string>? progress = null);
}
