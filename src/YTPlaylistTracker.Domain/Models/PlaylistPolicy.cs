using YTPlaylistTracker.Domain.Enums;

namespace YTPlaylistTracker.Domain.Models;

public record PlaylistPolicy(
    string Icon,
    int SortOrder,
    TimeSpan? ManualCooldown,
    bool AllowAutoSync,
    string? TrackingWarning)
{
    public static PlaylistPolicy For(PlaylistKind kind) => kind switch
    {
        PlaylistKind.Liked => new(
            Icon: "♥",
            SortOrder: 0,
            ManualCooldown: TimeSpan.FromDays(1),
            AllowAutoSync: false,
            TrackingWarning: "Liked Videos can contain thousands of videos.\n"
                           + "Initial sync may take a while.\n\n"
                           + "Enable tracking?"),
        PlaylistKind.WatchLater => new(
            Icon: "⏱",
            SortOrder: 1,
            ManualCooldown: null,
            AllowAutoSync: true,
            TrackingWarning: null),
        PlaylistKind.Uploads => new(
            Icon: "▶",
            SortOrder: 2,
            ManualCooldown: null,
            AllowAutoSync: true,
            TrackingWarning: null),
        _ => new(
            Icon: "",
            SortOrder: 10,
            ManualCooldown: null,
            AllowAutoSync: true,
            TrackingWarning: null)
    };

    public static PlaylistKind DetectKind(string youtubePlaylistId) => youtubePlaylistId switch
    {
        _ when youtubePlaylistId.StartsWith("LL", StringComparison.Ordinal) => PlaylistKind.Liked,
        _ when youtubePlaylistId.StartsWith("WL", StringComparison.Ordinal) => PlaylistKind.WatchLater,
        _ when youtubePlaylistId.StartsWith("UU", StringComparison.Ordinal) => PlaylistKind.Uploads,
        _ => PlaylistKind.Regular
    };
}
