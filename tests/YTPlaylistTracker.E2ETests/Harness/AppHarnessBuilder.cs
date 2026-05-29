using YTPlaylistTracker.Domain.Entities;

namespace YTPlaylistTracker.E2ETests.Harness;

internal sealed class AppHarnessBuilder
{
    private readonly List<Profile> _profiles = [];
    private readonly Dictionary<int, List<Playlist>> _playlistsByProfile = new();
    private readonly Dictionary<int, List<Video>> _videosByPlaylist = new();
    private readonly MockFactory _mocks = new();
    private string _themeName = "Catppuccin";
    private bool _autoSync;

    public AppHarnessBuilder WithProfile(Profile profile)
    {
        if (_profiles.Count == 0) profile.IsDefault = true;
        _profiles.Add(profile);
        return this;
    }

    public AppHarnessBuilder WithPlaylist(Profile profile, Playlist playlist)
    {
        if (!_playlistsByProfile.TryGetValue(profile.Id, out var list))
        {
            list = [];
            _playlistsByProfile[profile.Id] = list;
        }
        playlist.Profile = profile;
        list.Add(playlist);
        return this;
    }

    public AppHarnessBuilder WithVideo(Playlist playlist, Video video)
    {
        if (!_videosByPlaylist.TryGetValue(playlist.Id, out var list))
        {
            list = [];
            _videosByPlaylist[playlist.Id] = list;
        }
        video.Playlist = playlist;
        list.Add(video);
        return this;
    }

    public AppHarnessBuilder WithTheme(string name) { _themeName = name; return this; }
    public AppHarnessBuilder WithAutoSync(bool enabled = true) { _autoSync = enabled; return this; }
    public MockFactory Mocks => _mocks;

    public async Task<AppHarness> BuildAsync()
    {
        var profiles = _profiles.Count > 0
            ? _profiles
            : [new Profile { Id = 1, Name = "Default", IsDefault = true, IsOffline = true }];

        _mocks.ProfileRepo.GetAllAsync().Returns(profiles.AsReadOnly());
        _mocks.ProfileRepo.GetDefaultAsync().Returns(profiles.FirstOrDefault(p => p.IsDefault));

        foreach (var (profileId, playlists) in _playlistsByProfile)
            _mocks.PlaylistRepo.GetByProfileAsync(profileId).Returns(playlists.AsReadOnly());

        foreach (var (playlistId, videos) in _videosByPlaylist)
        {
            _mocks.PlaylistRepo.GetVideosAsync(playlistId).Returns(videos.AsReadOnly());
            // Mock the deleted-only path consistently with the videos provided, so the
            // F8 "show removed" view is deterministic (the default mock returns empty).
            var deleted = videos.Where(v => v.DeletedAt != null).ToList();
            _mocks.PlaylistRepo.GetDeletedVideosAsync(playlistId).Returns(deleted.AsReadOnly());
        }

        _mocks.UserSettings.ThemeName.Returns(_themeName);
        _mocks.UserSettings.AutoSyncOnStartup.Returns(_autoSync);

        return await AppHarness.CreateAsync(_mocks);
    }
}
