namespace YTPlaylistTracker.UI;

internal readonly record struct ColumnWidths(
    int NumberWidth,
    int TitleWidth,
    int ChannelWidth,
    int AddedWidth,
    int StatusWidth);

internal static class ColumnLayout
{
    private const int NumberFixed = 4;
    private const int AddedFixed = 12;
    private const int StatusFixed = 10;
    private const int Padding = 8;
    private const int TotalFixed = NumberFixed + AddedFixed + StatusFixed + Padding;

    private const double TitleRatio = 0.62;
    private const int TitleMin = 20;
    private const int ChannelMin = 10;

    public static ColumnWidths Compute(int availableWidth)
    {
        var flexible = Math.Max(0, availableWidth - TotalFixed);
        var titleRaw = (int)Math.Floor(flexible * TitleRatio);
        var channelRaw = flexible - titleRaw;

        var titleWidth = Math.Max(TitleMin, titleRaw);
        var channelWidth = Math.Max(ChannelMin, channelRaw);

        return new ColumnWidths(
            NumberFixed,
            titleWidth,
            channelWidth,
            AddedFixed,
            StatusFixed);
    }
}
