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
        return await db.Profiles.OrderBy(p => p.Name).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<Profile?> GetDefaultAsync(CancellationToken ct = default)
    {
        return await db.Profiles.FirstOrDefaultAsync(p => p.IsDefault, ct).ConfigureAwait(false);
    }

    public async Task<Profile?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        return await db.Profiles.FirstOrDefaultAsync(
            p => p.Name == name, ct).ConfigureAwait(false);
    }

    public async Task AddAsync(Profile profile, CancellationToken ct = default)
    {
        logger.LogInformation("Adding profile: {Name}", profile.Name);
        db.Profiles.Add(profile);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Profile profile, CancellationToken ct = default)
    {
        db.Profiles.Update(profile);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var profile = await db.Profiles.FindAsync([id], ct).ConfigureAwait(false);
        if (profile is not null)
        {
            logger.LogInformation("Deleting profile: {Name}", profile.Name);
            db.Profiles.Remove(profile);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task SetDefaultAsync(int id, CancellationToken ct = default)
    {
        await db.Profiles.ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDefault, valueExpression: false), ct).ConfigureAwait(false);
        await db.Profiles.Where(x => x.Id == id).ExecuteUpdateAsync(p => p.SetProperty(x => x.IsDefault, valueExpression: true), ct).ConfigureAwait(false);
        logger.LogInformation("Set default profile to ID {Id}", id);
    }
}
