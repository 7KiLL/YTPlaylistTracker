using YTPlaylistTracker.UI;

namespace YTPlaylistTracker.UnitTests.Helpers;

public class ColumnLayoutTests
{
    [Fact]
    public void Compute_WideTerminal_TitleGetsAllRemainingSpace()
    {
        // 100 cols: Fixed = 4+22+12+10+8 = 56, Title = 100-56 = 44
        var layout = ColumnLayout.Compute(100);

        Assert.Equal(4, layout.NumberWidth);
        Assert.Equal(44, layout.TitleWidth);
        Assert.Equal(22, layout.ChannelWidth);
        Assert.Equal(12, layout.AddedWidth);
        Assert.Equal(10, layout.StatusWidth);
    }

    [Fact]
    public void Compute_NarrowTerminal_TitleClampsToMinimum()
    {
        // 50 cols: Fixed = 56, Title = 50-56 = -6 → clamped to 20
        var layout = ColumnLayout.Compute(50);

        Assert.Equal(20, layout.TitleWidth);
        Assert.Equal(22, layout.ChannelWidth);
    }

    [Fact]
    public void Compute_MediumTerminal_TitleGetsRemainder()
    {
        // 80 cols: Title = 80-56 = 24
        var layout = ColumnLayout.Compute(80);

        Assert.Equal(24, layout.TitleWidth);
        Assert.Equal(22, layout.ChannelWidth);
    }

    [Fact]
    public void Compute_VeryWideTerminal_TitleExpandsFully()
    {
        // 150 cols: Title = 150-56 = 94
        var layout = ColumnLayout.Compute(150);

        Assert.Equal(94, layout.TitleWidth);
        Assert.Equal(22, layout.ChannelWidth);
    }

    [Fact]
    public void Compute_ZeroWidth_TitleReturnsMinimum()
    {
        var layout = ColumnLayout.Compute(0);

        Assert.Equal(20, layout.TitleWidth);
        Assert.Equal(22, layout.ChannelWidth);
    }

    [Fact]
    public void Compute_FixedColumnsNeverChange()
    {
        foreach (var width in new[] { 0, 50, 80, 100, 150, 200 })
        {
            var layout = ColumnLayout.Compute(width);
            Assert.Equal(4, layout.NumberWidth);
            Assert.Equal(22, layout.ChannelWidth);
            Assert.Equal(12, layout.AddedWidth);
            Assert.Equal(10, layout.StatusWidth);
        }
    }
}
