using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Infrastructure.Data;

namespace YTPlaylistTracker.IntegrationTests.Data;

public class ProfileRepositoryTests
{
    private AppDbContext _db = null!;
    private ProfileRepository _repo = null!;

    [Before(Test)]
    public void Setup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        var logger = Substitute.For<ILogger<ProfileRepository>>();
        _repo = new ProfileRepository(_db, logger);
    }

    [Test]
    public async Task GetByNameAsync_ReturnsMatchingProfile()
    {
        await _repo.AddAsync(new Profile { Name = "Work", IsDefault = true });

        var result = await _repo.GetByNameAsync("Work");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("Work");
    }

    [Test]
    public async Task GetByNameAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _repo.GetByNameAsync("NonExistent");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetByNameAsync_IsCaseSensitive()
    {
        await _repo.AddAsync(new Profile { Name = "Work", IsDefault = true });

        await Assert.That(await _repo.GetByNameAsync("work")).IsNull();
        await Assert.That(await _repo.GetByNameAsync("Work")).IsNotNull();
    }

    [After(Test)]
    public void Cleanup()
    {
        _db.Dispose();
    }
}
