using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Microsoft.Extensions.Logging;
using YTPlaylistTracker.Application.Services;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;

namespace YTPlaylistTracker.UnitTests.Services;

public class SyncServiceErrorTests
{
    private readonly IYouTubeApiService _youtubeApi = Substitute.For<IYouTubeApiService>();
    private readonly IPlaylistRepository _playlistRepo = Substitute.For<IPlaylistRepository>();
    private readonly ISyncService _syncService;

    private static readonly Profile _testProfile = new() { Name = "test" };

    private readonly Playlist _testPlaylist = new()
    {
        Id = 1,
        ProfileId = 1,
        YouTubePlaylistId = "PLtest123",
        Title = "Test Playlist",
        IsTracked = true,
        Profile = _testProfile
    };

    public SyncServiceErrorTests()
    {
        var logger = Substitute.For<ILogger<SyncService>>();
        _syncService = new SyncService(_playlistRepo, logger);
    }

    [Fact]
    public async Task SyncPlaylistAsync_WhenYouTubeApiThrows_PropagatesException()
    {
        _youtubeApi.GetPlaylistVideosAsync("PLtest123")
            .ThrowsAsync(new HttpRequestException("Network error"));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => _syncService.SyncPlaylistAsync(_testPlaylist, _youtubeApi));
    }

    [Fact]
    public async Task SyncAllTrackedAsync_WhenOnePlaylistFails_ContinuesWithOthers()
    {
        var playlist1 = new Playlist { Id = 1, ProfileId = 1, YouTubePlaylistId = "PL1", Title = "P1", IsTracked = true, Profile = _testProfile };
        var playlist2 = new Playlist { Id = 2, ProfileId = 1, YouTubePlaylistId = "PL2", Title = "P2", IsTracked = true, Profile = _testProfile };

        _playlistRepo.GetTrackedByProfileAsync(1).Returns(new List<Playlist> { playlist1, playlist2 });

        // First playlist throws
        _youtubeApi.GetPlaylistVideosAsync("PL1")
            .ThrowsAsync(new HttpRequestException("API error"));

        // Second playlist succeeds
        _youtubeApi.GetPlaylistVideosAsync("PL2").Returns(new List<Domain.Models.YouTubeVideoSnapshot>());
        _playlistRepo.GetVideosAsync(2).Returns(new List<Video>());
        _youtubeApi.GetPlaylistMetadataAsync("PL2").Returns((Domain.Models.YouTubePlaylistSnapshot?)null);

        var results = await _syncService.SyncAllTrackedAsync(1, _youtubeApi);

        Assert.Equal(2, results.Count);
        Assert.Equal(0, results[1].Added); // Failed playlist gets zero result
        Assert.Equal(0, results[2].Added); // Second playlist was synced
    }

    [Fact]
    public async Task SyncAllTrackedAsync_WhenAllPlaylistsFail_ReturnsZeroResults()
    {
        var playlist1 = new Playlist { Id = 1, ProfileId = 1, YouTubePlaylistId = "PL1", Title = "P1", IsTracked = true, Profile = _testProfile };

        _playlistRepo.GetTrackedByProfileAsync(1).Returns(new List<Playlist> { playlist1 });

        _youtubeApi.GetPlaylistVideosAsync("PL1")
            .ThrowsAsync(new InvalidOperationException("Auth failed"));

        var results = await _syncService.SyncAllTrackedAsync(1, _youtubeApi);

        Assert.Single(results);
        Assert.Equal(0, results[1].Added);
        Assert.Equal(0, results[1].Removed);
        Assert.Equal(0, results[1].Updated);
    }
}
