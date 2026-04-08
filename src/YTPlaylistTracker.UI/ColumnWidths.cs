using System.Runtime.InteropServices;

namespace YTPlaylistTracker.UI;

[StructLayout(LayoutKind.Auto)]
internal readonly record struct ColumnWidths(
    int NumberWidth,
    int TitleWidth,
    int ChannelWidth,
    int AddedWidth,
    int StatusWidth);
