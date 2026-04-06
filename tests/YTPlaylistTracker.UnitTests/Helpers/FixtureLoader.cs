using System.Text.Json;
using YTPlaylistTracker.Domain.Models;

namespace YTPlaylistTracker.UnitTests.Helpers;

public static class FixtureLoader
{
    private static readonly string FixturesDir = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "tests", "fixtures");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static List<YouTubeVideoSnapshot> LoadPlaylistVideos(string playlistId)
    {
        var path = Path.Combine(Path.GetFullPath(FixturesDir), $"playlist_videos_{playlistId}.json");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Fixture not found: {path}");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<YouTubeVideoSnapshot>>(json, JsonOptions)
               ?? throw new InvalidOperationException($"Failed to deserialize fixture: {path}");
    }

    public static List<YouTubePlaylistSnapshot> LoadUserPlaylists()
    {
        var path = Path.Combine(Path.GetFullPath(FixturesDir), "user_playlists.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<YouTubePlaylistSnapshot>>(json, JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize user_playlists.json");
    }
}
