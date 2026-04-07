using System.Text;
using System.Text.Json;
using YTPlaylistTracker.Domain.Entities;

namespace YTPlaylistTracker.Application.Services;

public static class ExportService
{
    public record ExportEntry(
        string PlaylistTitle,
        string VideoId,
        string VideoTitle,
        string Channel,
        string RemovalReason,
        string RemovedAt);

    public static List<ExportEntry> BuildEntries(List<(Playlist Playlist, Video Video)> removedVideos)
    {
        return removedVideos.Select(pv => new ExportEntry(
            pv.Playlist.Title ?? pv.Playlist.YouTubePlaylistId,
            pv.Video.YouTubeVideoId,
            pv.Video.Title,
            pv.Video.ChannelTitle ?? "",
            pv.Video.RemovalReason?.ToString() ?? "Unknown",
            pv.Video.DeletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
        )).ToList();
    }

    public static string ToCsv(List<ExportEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PlaylistTitle,VideoId,VideoTitle,Channel,RemovalReason,RemovedAt");
        foreach (var e in entries)
        {
            sb.AppendLine($"{Escape(e.PlaylistTitle)},{Escape(e.VideoId)},{Escape(e.VideoTitle)},{Escape(e.Channel)},{Escape(e.RemovalReason)},{Escape(e.RemovedAt)}");
        }
        return sb.ToString();
    }

    public static string ToJson(List<ExportEntry> entries)
    {
        return JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string Escape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
