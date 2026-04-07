using YTPlaylistTracker.Application.Services;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Enums;

namespace YTPlaylistTracker.UnitTests.Services;

public class ExportServiceTests
{
    private static readonly Profile _testProfile = new() { Name = "test" };

    private static List<(Playlist Playlist, Video Video)> CreateTestData()
    {
        var playlist1 = new Playlist { Id = 1, ProfileId = 1, YouTubePlaylistId = "PL1", Title = "My Favorites", Profile = _testProfile };
        var playlist2 = new Playlist { Id = 2, ProfileId = 1, YouTubePlaylistId = "PL2", Title = "Watch Later", Profile = _testProfile };

        return
        [
            (playlist1, new Video
            {
                PlaylistId = 1, YouTubeVideoId = "abc123", Title = "Deleted Video",
                ChannelTitle = "TestChannel", DeletedAt = new DateTime(2025, 6, 15, 10, 30, 0),
                RemovalReason = RemovalReason.Deleted, Playlist = playlist1
            }),
            (playlist2, new Video
            {
                PlaylistId = 2, YouTubeVideoId = "def456", Title = "Private Video",
                ChannelTitle = "OtherChannel", DeletedAt = new DateTime(2025, 7, 1, 14, 0, 0),
                RemovalReason = RemovalReason.Private, Playlist = playlist2
            }),
        ];
    }

    [Fact]
    public void BuildEntries_MapsFieldsCorrectly()
    {
        var data = CreateTestData();
        var entries = ExportService.BuildEntries(data);

        Assert.Equal(2, entries.Count);
        Assert.Equal("My Favorites", entries[0].PlaylistTitle);
        Assert.Equal("abc123", entries[0].VideoId);
        Assert.Equal("Deleted Video", entries[0].VideoTitle);
        Assert.Equal("TestChannel", entries[0].Channel);
        Assert.Equal("Deleted", entries[0].RemovalReason);
        Assert.Equal("2025-06-15 10:30:00", entries[0].RemovedAt);
    }

    [Fact]
    public void BuildEntries_UsesPlaylistIdWhenTitleNull()
    {
        var playlist = new Playlist { Id = 1, ProfileId = 1, YouTubePlaylistId = "PLnoTitle", Title = null, Profile = _testProfile };
        var video = new Video
        {
            PlaylistId = 1, YouTubeVideoId = "v1", Title = "Some Video",
            DeletedAt = DateTime.UtcNow, RemovalReason = RemovalReason.Deleted, Playlist = playlist
        };

        var entries = ExportService.BuildEntries([(playlist, video)]);

        Assert.Equal("PLnoTitle", entries[0].PlaylistTitle);
    }

    [Fact]
    public void BuildEntries_HandlesNullChannelAndReason()
    {
        var playlist = new Playlist { Id = 1, ProfileId = 1, YouTubePlaylistId = "PL1", Title = "Test", Profile = _testProfile };
        var video = new Video
        {
            PlaylistId = 1, YouTubeVideoId = "v1", Title = "Video",
            ChannelTitle = null, DeletedAt = DateTime.UtcNow, RemovalReason = null, Playlist = playlist
        };

        var entries = ExportService.BuildEntries([(playlist, video)]);

        Assert.Equal("", entries[0].Channel);
        Assert.Equal("Unknown", entries[0].RemovalReason);
    }

    [Fact]
    public void ToCsv_ContainsHeaderAndRows()
    {
        var entries = ExportService.BuildEntries(CreateTestData());
        var csv = ExportService.ToCsv(entries);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length); // header + 2 rows
        Assert.Equal("PlaylistTitle,VideoId,VideoTitle,Channel,RemovalReason,RemovedAt", lines[0]);
        Assert.Contains("abc123", lines[1]);
        Assert.Contains("def456", lines[2]);
    }

    [Fact]
    public void ToCsv_EscapesCommasAndQuotes()
    {
        var playlist = new Playlist { Id = 1, ProfileId = 1, YouTubePlaylistId = "PL1", Title = "Rock, Pop & Jazz", Profile = _testProfile };
        var video = new Video
        {
            PlaylistId = 1, YouTubeVideoId = "v1", Title = "He said \"hello\"",
            ChannelTitle = "Ch", DeletedAt = new DateTime(2025, 1, 1), RemovalReason = RemovalReason.Deleted, Playlist = playlist
        };

        var entries = ExportService.BuildEntries([(playlist, video)]);
        var csv = ExportService.ToCsv(entries);

        // Commas in playlist title should be quoted
        Assert.Contains("\"Rock, Pop & Jazz\"", csv);
        // Quotes in video title should be doubled and quoted
        Assert.Contains("\"He said \"\"hello\"\"\"", csv);
    }

    [Fact]
    public void ToCsv_EmptyList_ReturnsHeaderOnly()
    {
        var csv = ExportService.ToCsv([]);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Single(lines);
        Assert.StartsWith("PlaylistTitle", lines[0]);
    }

    [Fact]
    public void ToJson_ValidJson_DeserializesBackCorrectly()
    {
        var entries = ExportService.BuildEntries(CreateTestData());
        var json = ExportService.ToJson(entries);

        var deserialized = System.Text.Json.JsonSerializer.Deserialize<List<ExportService.ExportEntry>>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Count);
        Assert.Equal("abc123", deserialized[0].VideoId);
        Assert.Equal("Private", deserialized[1].RemovalReason);
    }

    [Fact]
    public void ToJson_EmptyList_ReturnsEmptyArray()
    {
        var json = ExportService.ToJson([]);
        Assert.Equal("[]", json);
    }
}
