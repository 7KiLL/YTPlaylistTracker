using YTPlaylistTracker.Domain.Entities;

namespace YTPlaylistTracker.E2ETests.Builders;

internal sealed class PlaylistBuilder
{
    private int _id = 1;
    private string _youtubeId = "PLtest001";
    private string _title = "Test Playlist";
    private bool _isTracked = true;

    public PlaylistBuilder WithId(int id) { _id = id; return this; }
    public PlaylistBuilder WithYouTubeId(string id) { _youtubeId = id; return this; }
    public PlaylistBuilder WithTitle(string title) { _title = title; return this; }
    public PlaylistBuilder Tracked(bool tracked = true) { _isTracked = tracked; return this; }

    public Playlist Build(Profile profile) => new()
    {
        Id = _id,
        ProfileId = profile.Id,
        Profile = profile,
        YouTubePlaylistId = _youtubeId,
        Title = _title,
        IsTracked = _isTracked,
    };
}
