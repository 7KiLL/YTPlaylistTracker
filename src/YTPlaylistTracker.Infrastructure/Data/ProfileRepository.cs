using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;

namespace YTPlaylistTracker.Infrastructure.Data;

public class ProfileRepository(AppDbContext db, ILogger<ProfileRepository> logger) : IProfileRepository
{
    public async Task<IReadOnlyList<Profile>> GetAllAsync(CancellationToken ct = default)
    {
        logger.LogDebug("Fetching all profiles");
        return await db.Profiles.OrderBy(p => p.Name).ToListAsync(ct);
    }

    public async Task<Profile?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.Profiles.FindAsync([id], ct);
    }

    public async Task<Profile?> GetDefaultAsync(CancellationToken ct = default)
    {
        return await db.Profiles.FirstOrDefaultAsync(p => p.IsDefault, ct);
    }

    public async Task AddAsync(Profile profile, CancellationToken ct = default)
    {
        logger.LogInformation("Adding profile: {Name}", profile.Name);
        db.Profiles.Add(profile);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Profile profile, CancellationToken ct = default)
    {
        db.Profiles.Update(profile);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var profile = await db.Profiles.FindAsync([id], ct);
        if (profile is not null)
        {
            logger.LogInformation("Deleting profile: {Name}", profile.Name);
            db.Profiles.Remove(profile);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task SetDefaultAsync(int id, CancellationToken ct = default)
    {
        await db.Profiles.ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDefault, false), ct);
        await db.Profiles.Where(x => x.Id == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDefault, true), ct);
        logger.LogInformation("Set default profile to ID {Id}", id);
    }
}
