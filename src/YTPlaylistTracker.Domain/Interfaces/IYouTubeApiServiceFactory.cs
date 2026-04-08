using YTPlaylistTracker.Domain.Entities;

namespace YTPlaylistTracker.Domain.Interfaces;

public interface IYouTubeApiServiceFactory
{
    Task<IYouTubeApiService> CreateForProfileAsync(Profile profile);
    Task<IYouTubeApiService> LoginAsync(Profile profile, Action<string>? onAuthUrl = null);
    bool IsAuthenticated(Profile profile);
}
