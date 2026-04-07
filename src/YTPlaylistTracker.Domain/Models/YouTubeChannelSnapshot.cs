namespace YTPlaylistTracker.Domain.Models;

public record YouTubeChannelSnapshot(
    string ChannelId,
    string Title,
    string? ThumbnailUrl,
    string? LikedVideosPlaylistId = null);
