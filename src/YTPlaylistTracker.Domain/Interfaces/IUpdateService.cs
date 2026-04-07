using YTPlaylistTracker.Domain.Models;

namespace YTPlaylistTracker.Domain.Interfaces;

public interface IUpdateService
{
    Task<UpdateInfo> CheckForUpdateAsync();
    Task<string> ApplyUpdateAsync(UpdateInfo update);
}
