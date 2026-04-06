using YTPlaylistTracker.Domain.Entities;

namespace YTPlaylistTracker.Domain.Interfaces;

public interface IProfileRepository
{
    Task<List<Profile>> GetAllAsync();
    Task<Profile?> GetByIdAsync(int id);
    Task<Profile?> GetDefaultAsync();
    Task AddAsync(Profile profile);
    Task UpdateAsync(Profile profile);
    Task DeleteAsync(int id);
    Task SetDefaultAsync(int id);
}
