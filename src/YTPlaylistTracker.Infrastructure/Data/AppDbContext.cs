using Microsoft.EntityFrameworkCore;
using YTPlaylistTracker.Domain.Entities;

namespace YTPlaylistTracker.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Playlist> Playlists => Set<Playlist>();
    public DbSet<Video> Videos => Set<Video>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Profile>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired().HasMaxLength(100);
            e.HasMany(p => p.Playlists).WithOne(pl => pl.Profile).HasForeignKey(pl => pl.ProfileId);
        });

        modelBuilder.Entity<Playlist>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.YouTubePlaylistId).IsRequired().HasMaxLength(100);
            e.HasIndex(p => new { p.ProfileId, p.YouTubePlaylistId }).IsUnique();
            e.HasMany(p => p.Videos).WithOne(v => v.Playlist).HasForeignKey(v => v.PlaylistId);
        });

        modelBuilder.Entity<Video>(e =>
        {
            e.HasKey(v => v.Id);
            e.Property(v => v.YouTubeVideoId).IsRequired().HasMaxLength(20);
            e.Property(v => v.Title).IsRequired().HasMaxLength(500);
            e.Property(v => v.ChannelTitle).HasMaxLength(200);
            e.HasIndex(v => new { v.PlaylistId, v.YouTubeVideoId }).IsUnique();
        });
    }
}
