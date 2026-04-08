using System.Text;
using System.Text.Json;
using YTPlaylistTracker.Domain.Entities;

namespace YTPlaylistTracker.Application.Services;

public static class ExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public record ExportEntry(
        string PlaylistTitle,
        string VideoId,
        string VideoTitle,
        string Channel,
        string RemovalReason,
        string RemovedAt);

    public static IReadOnlyList<ExportEntry> BuildEntries(IReadOnlyList<(Playlist Playlist, Video Video)> removedVideos)
    {
        return [..removedVideos.Select(pv => new ExportEntry(
            pv.Playlist.Title ?? pv.Playlist.YouTubePlaylistId,
            pv.Video.YouTubeVideoId,
            pv.Video.Title,
            pv.Video.ChannelTitle ?? "",
            pv.Video.RemovalReason?.ToString() ?? "Unknown",
            pv.Video.DeletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? ""
        )),];
    }

    public static string ToCsv(IReadOnlyList<ExportEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine("PlaylistTitle,VideoId,VideoTitle,Channel,RemovalReason,RemovedAt");
        foreach (var e in entries)
        {
            sb.AppendLine($"{Escape(e.PlaylistTitle)},{Escape(e.VideoId)},{Escape(e.VideoTitle)},{Escape(e.Channel)},{Escape(e.RemovalReason)},{Escape(e.RemovedAt)}");
        }
        return sb.ToString();
    }

    public static string ToJson(IReadOnlyList<ExportEntry> entries)
    {
        return JsonSerializer.Serialize(entries, JsonOptions);
    }

    private static string Escape(string value)
    {
        if (value.AsSpan().IndexOfAny(['"', ',', '\n']) >= 0)
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        return value;
    }
}
