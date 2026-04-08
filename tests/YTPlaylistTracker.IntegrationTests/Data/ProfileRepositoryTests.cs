using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Infrastructure.Data;

namespace YTPlaylistTracker.IntegrationTests.Data;

public class ProfileRepositoryTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ProfileRepository _repo;

    public ProfileRepositoryTests()
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

    [Fact]
    public async Task GetByNameAsync_ReturnsMatchingProfile()
    {
        await _repo.AddAsync(new Profile { Name = "Work", IsDefault = true });

        var result = await _repo.GetByNameAsync("Work");

        Assert.NotNull(result);
        Assert.Equal("Work", result.Name);
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _repo.GetByNameAsync("NonExistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByNameAsync_IsCaseSensitive()
    {
        await _repo.AddAsync(new Profile { Name = "Work", IsDefault = true });

        Assert.Null(await _repo.GetByNameAsync("work"));
        Assert.NotNull(await _repo.GetByNameAsync("Work"));
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
