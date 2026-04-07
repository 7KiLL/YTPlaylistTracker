namespace YTPlaylistTracker.Domain.Entities;

public class Playlist
{
    public int Id { get; init; }
    public int ProfileId { get; init; }
    public required string YouTubePlaylistId { get; set; }
    public string? Title { get; set; }
    public bool IsTracked { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public string? JsonMetadata { get; set; }
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public DateTime? PublishedAt { get; set; }

    public required Profile Profile { get; set; }
    public ICollection<Video> Videos { get; set; } = [];
}
