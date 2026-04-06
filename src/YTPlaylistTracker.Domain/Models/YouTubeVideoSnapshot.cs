namespace YTPlaylistTracker.Domain.Models;

public record YouTubeVideoSnapshot(
    string VideoId,
    string Title,
    string? ChannelTitle,
    int Position,
    DateTime? AddedAt = null,
    string? Description = null,
    string? ThumbnailUrl = null,
    string? JsonMetadata = null);
