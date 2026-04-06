namespace YTPlaylistTracker.Domain.Models;

public record YouTubePlaylistSnapshot(
    string PlaylistId,
    string Title,
    string? ChannelTitle,
    string? Description = null,
    string? ThumbnailUrl = null,
    DateTime? PublishedAt = null,
    string? JsonMetadata = null);
