using YTPlaylistTracker.Domain.Entities;

namespace YTPlaylistTracker.Domain.Interfaces;

public interface IProfileRepository
{
    Task<IReadOnlyList<Profile>> GetAllAsync(CancellationToken ct = default);
    Task<Profile?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Profile?> GetDefaultAsync(CancellationToken ct = default);
    Task AddAsync(Profile profile, CancellationToken ct = default);
    Task UpdateAsync(Profile profile, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task SetDefaultAsync(int id, CancellationToken ct = default);
}
