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

    [Test]
    public async Task BuildEntries_MapsFieldsCorrectly()
    {
        var data = CreateTestData();
        var entries = ExportService.BuildEntries(data);

        await Assert.That(entries.Count).IsEqualTo(2);
        await Assert.That(entries[0].PlaylistTitle).IsEqualTo("My Favorites");
        await Assert.That(entries[0].VideoId).IsEqualTo("abc123");
        await Assert.That(entries[0].VideoTitle).IsEqualTo("Deleted Video");
        await Assert.That(entries[0].Channel).IsEqualTo("TestChannel");
        await Assert.That(entries[0].RemovalReason).IsEqualTo("Deleted");
        await Assert.That(entries[0].RemovedAt).IsEqualTo("2025-06-15 10:30:00");
    }

    [Test]
    public async Task BuildEntries_UsesPlaylistIdWhenTitleNull()
    {
        var playlist = new Playlist { Id = 1, ProfileId = 1, YouTubePlaylistId = "PLnoTitle", Title = null, Profile = _testProfile };
        var video = new Video
        {
            PlaylistId = 1, YouTubeVideoId = "v1", Title = "Some Video",
            DeletedAt = DateTime.UtcNow, RemovalReason = RemovalReason.Deleted, Playlist = playlist
        };

        var entries = ExportService.BuildEntries([(playlist, video)]);

        await Assert.That(entries[0].PlaylistTitle).IsEqualTo("PLnoTitle");
    }

    [Test]
    public async Task BuildEntries_HandlesNullChannelAndReason()
    {
        var playlist = new Playlist { Id = 1, ProfileId = 1, YouTubePlaylistId = "PL1", Title = "Test", Profile = _testProfile };
        var video = new Video
        {
            PlaylistId = 1, YouTubeVideoId = "v1", Title = "Video",
            ChannelTitle = null, DeletedAt = DateTime.UtcNow, RemovalReason = null, Playlist = playlist
        };

        var entries = ExportService.BuildEntries([(playlist, video)]);

        await Assert.That(entries[0].Channel).IsEqualTo("");
        await Assert.That(entries[0].RemovalReason).IsEqualTo("Unknown");
    }

    [Test]
    public async Task ToCsv_ContainsHeaderAndRows()
    {
        var entries = ExportService.BuildEntries(CreateTestData());
        var csv = ExportService.ToCsv(entries);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        await Assert.That(lines.Length).IsEqualTo(3); // header + 2 rows
        await Assert.That(lines[0]).IsEqualTo("PlaylistTitle,VideoId,VideoTitle,Channel,RemovalReason,RemovedAt");
        await Assert.That(lines[1]).Contains("abc123");
        await Assert.That(lines[2]).Contains("def456");
    }

    [Test]
    public async Task ToCsv_EscapesCommasAndQuotes()
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
        await Assert.That(csv).Contains("\"Rock, Pop & Jazz\"");
        // Quotes in video title should be doubled and quoted
        await Assert.That(csv).Contains("\"He said \"\"hello\"\"\"");
    }

    [Test]
    public async Task ToCsv_EmptyList_ReturnsHeaderOnly()
    {
        var csv = ExportService.ToCsv([]);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        await Assert.That(lines).HasSingleItem();
        await Assert.That(lines[0]).StartsWith("PlaylistTitle");
    }

    [Test]
    public async Task ToJson_ValidJson_DeserializesBackCorrectly()
    {
        var entries = ExportService.BuildEntries(CreateTestData());
        var json = ExportService.ToJson(entries);

        var deserialized = System.Text.Json.JsonSerializer.Deserialize<List<ExportService.ExportEntry>>(json);
        await Assert.That(deserialized).IsNotNull();
        await Assert.That(deserialized!.Count).IsEqualTo(2);
        await Assert.That(deserialized[0].VideoId).IsEqualTo("abc123");
        await Assert.That(deserialized[1].RemovalReason).IsEqualTo("Private");
    }

    [Test]
    public async Task ToJson_EmptyList_ReturnsEmptyArray()
    {
        var json = ExportService.ToJson([]);
        await Assert.That(json).IsEqualTo("[]");
    }
}
