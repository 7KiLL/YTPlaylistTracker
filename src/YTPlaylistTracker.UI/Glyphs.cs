using YTPlaylistTracker.Domain.Enums;
using YTPlaylistTracker.Domain.Models;

namespace YTPlaylistTracker.UI;

internal static class Glyphs
{
    private static GlyphMode Mode => GlyphDetector.Detect();

    internal static string PlaylistIcon(PlaylistKind kind) => Mode switch
    {
        GlyphMode.Full => PlaylistPolicy.For(kind).Icon,
        _ => kind switch
        {
            PlaylistKind.Liked => "*",
            PlaylistKind.WatchLater => "@",
            PlaylistKind.Uploads => "#",
            _ => "",
        },
    };

    internal static string[] SpinnerFrames => Mode switch
    {
        GlyphMode.Full => ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"],
        _ => ["|", "/", "-", "\\"],
    };

    internal static string SortAscending => Mode == GlyphMode.Full ? " ▲" : " ^";

    internal static string SortDescending => Mode == GlyphMode.Full ? " ▼" : " v";

    internal static string DefaultMarker => Mode == GlyphMode.Full ? "▸ " : "> ";

    internal static string Check => Mode == GlyphMode.Full ? "✓" : "+";

    // Status indicators (ASCII-safe, no Full/Basic distinction needed)
    internal static string Tracked => "[x] ";
    internal static string Untracked => "[ ] ";
    internal static string ManualOnly => "    ";
    internal static string StatusActive => "Active";
    internal static string StatusRemoved => "X ";
    internal static string StatusRemovedFallback => "Removed";
    internal static string OfflineSuffix => " (offline)";
}
