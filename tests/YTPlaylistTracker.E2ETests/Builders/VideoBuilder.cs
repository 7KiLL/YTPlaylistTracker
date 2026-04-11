using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Enums;

namespace YTPlaylistTracker.E2ETests.Builders;

internal sealed class VideoBuilder
{
    private int _id = 1;
    private string _youtubeId = "vid001";
    private string _title = "Test Video";
    private string? _channel = "Test Channel";
    private DateTime? _addedAt = DateTime.UtcNow.AddDays(-7);
    private DateTime? _deletedAt;
    private RemovalReason? _removalReason;

    public VideoBuilder WithId(int id) { _id = id; return this; }
    public VideoBuilder WithYouTubeId(string id) { _youtubeId = id; return this; }
    public VideoBuilder WithTitle(string title) { _title = title; return this; }
    public VideoBuilder WithChannel(string channel) { _channel = channel; return this; }
    public VideoBuilder Deleted(RemovalReason reason = RemovalReason.Deleted)
    {
        _deletedAt = DateTime.UtcNow.AddDays(-1);
        _removalReason = reason;
        return this;
    }

    public Video Build(Playlist playlist) => new()
    {
        Id = _id,
        PlaylistId = playlist.Id,
        Playlist = playlist,
        YouTubeVideoId = _youtubeId,
        Title = _title,
        ChannelTitle = _channel,
        AddedAt = _addedAt,
        DeletedAt = _deletedAt,
        RemovalReason = _removalReason,
    };
}
