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
    private const int ChannelFixed = 22;
    private const int AddedFixed = 12;
    private const int StatusFixed = 10;
    private const int Padding = 8; // cell separators + margins
    private const int FixedTotal = NumberFixed + ChannelFixed + AddedFixed + StatusFixed + Padding;

    private const int TitleMin = 20;

    public static ColumnWidths Compute(int availableWidth)
    {
        var titleWidth = Math.Max(TitleMin, availableWidth - FixedTotal);

        return new ColumnWidths(
            NumberFixed,
            titleWidth,
            ChannelFixed,
            AddedFixed,
            StatusFixed);
    }
}
