namespace YTPlaylistTracker.Domain.Entities;

public class Profile
{
    public int Id { get; init; }
    public required string Name { get; set; }
    public string? YouTubeChannelId { get; set; }
    public string? ChannelTitle { get; set; }
    public string? ChannelThumbnailUrl { get; set; }
    public string? OAuthTokenPath { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Playlist> Playlists { get; set; } = [];
}
