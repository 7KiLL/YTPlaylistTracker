using YTPlaylistTracker.UI;

namespace YTPlaylistTracker.UnitTests.Helpers;

public class ColumnLayoutTests
{
    [Fact]
    public void Compute_WideTerminal_AllocatesProportionally()
    {
        var layout = ColumnLayout.Compute(100);

        Assert.Equal(4, layout.NumberWidth);
        Assert.Equal(40, layout.TitleWidth);
        Assert.Equal(26, layout.ChannelWidth);
        Assert.Equal(12, layout.AddedWidth);
        Assert.Equal(10, layout.StatusWidth);
    }

    [Fact]
    public void Compute_NarrowTerminal_ClampsToMinimums()
    {
        var layout = ColumnLayout.Compute(50);

        Assert.Equal(4, layout.NumberWidth);
        Assert.Equal(20, layout.TitleWidth);
        Assert.Equal(10, layout.ChannelWidth);
        Assert.Equal(12, layout.AddedWidth);
        Assert.Equal(10, layout.StatusWidth);
    }

    [Fact]
    public void Compute_MediumTerminal_RespectsRatio()
    {
        var layout = ColumnLayout.Compute(80);

        Assert.Equal(28, layout.TitleWidth);
        Assert.Equal(18, layout.ChannelWidth);
    }

    [Fact]
    public void Compute_VeryWideTerminal_ScalesUp()
    {
        var layout = ColumnLayout.Compute(150);

        Assert.Equal(71, layout.TitleWidth);
        Assert.Equal(45, layout.ChannelWidth);
    }

    [Fact]
    public void Compute_ZeroWidth_ReturnsMinimums()
    {
        var layout = ColumnLayout.Compute(0);

        Assert.Equal(20, layout.TitleWidth);
        Assert.Equal(10, layout.ChannelWidth);
    }
}
