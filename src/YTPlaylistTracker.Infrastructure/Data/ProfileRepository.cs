using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;

namespace YTPlaylistTracker.Infrastructure.Data;

public class ProfileRepository(AppDbContext db, ILogger<ProfileRepository> logger) : IProfileRepository
{
    public async Task<List<Profile>> GetAllAsync()
    {
        logger.LogDebug("Fetching all profiles");
        return await db.Profiles.OrderBy(p => p.Name).ToListAsync();
    }

    public async Task<Profile?> GetByIdAsync(int id)
    {
        return await db.Profiles.FindAsync(id);
    }

    public async Task<Profile?> GetDefaultAsync()
    {
        return await db.Profiles.FirstOrDefaultAsync(p => p.IsDefault);
    }

    public async Task AddAsync(Profile profile)
    {
        logger.LogInformation("Adding profile: {Name}", profile.Name);
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Profile profile)
    {
        db.Profiles.Update(profile);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var profile = await db.Profiles.FindAsync(id);
        if (profile is not null)
        {
            logger.LogInformation("Deleting profile: {Name}", profile.Name);
            db.Profiles.Remove(profile);
            await db.SaveChangesAsync();
        }
    }

    public async Task SetDefaultAsync(int id)
    {
        var profiles = await db.Profiles.ToListAsync();
        foreach (var p in profiles)
            p.IsDefault = p.Id == id;
        await db.SaveChangesAsync();
        logger.LogInformation("Set default profile to ID {Id}", id);
    }
}
