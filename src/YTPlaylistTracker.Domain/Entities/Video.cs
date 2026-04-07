using YTPlaylistTracker.Domain.Enums;

namespace YTPlaylistTracker.Domain.Entities;

public class Video
{
    public int Id { get; init; }
    public int PlaylistId { get; init; }
    public required string YouTubeVideoId { get; set; }
    public required string Title { get; set; }
    public string? ChannelTitle { get; set; }
    public RemovalReason? RemovalReason { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? AddedAt { get; set; }
    public string? Description { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int Position { get; set; }
    /// <summary>Raw YouTube API snippet JSON for rich detail views.</summary>
    public string? JsonMetadata { get; set; }

    public required Playlist Playlist { get; set; }
}
