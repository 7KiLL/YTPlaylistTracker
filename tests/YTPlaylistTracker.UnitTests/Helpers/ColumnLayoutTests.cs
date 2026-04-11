using YTPlaylistTracker.UI;

namespace YTPlaylistTracker.UnitTests.Helpers;

public class ColumnLayoutTests
{
    [Test]
    public async Task Compute_WideTerminal_TitleGetsAllRemainingSpace()
    {
        // 100 cols: Fixed = 4+22+12+10+8 = 56, Title = 100-56 = 44
        var layout = ColumnLayout.Compute(100);

        await Assert.That(layout.NumberWidth).IsEqualTo(4);
        await Assert.That(layout.TitleWidth).IsEqualTo(44);
        await Assert.That(layout.ChannelWidth).IsEqualTo(22);
        await Assert.That(layout.AddedWidth).IsEqualTo(12);
        await Assert.That(layout.StatusWidth).IsEqualTo(10);
    }

    [Test]
    public async Task Compute_NarrowTerminal_TitleClampsToMinimum()
    {
        // 50 cols: Fixed = 56, Title = 50-56 = -6 -> clamped to 20
        var layout = ColumnLayout.Compute(50);

        await Assert.That(layout.TitleWidth).IsEqualTo(20);
        await Assert.That(layout.ChannelWidth).IsEqualTo(22);
    }

    [Test]
    public async Task Compute_MediumTerminal_TitleGetsRemainder()
    {
        // 80 cols: Title = 80-56 = 24
        var layout = ColumnLayout.Compute(80);

        await Assert.That(layout.TitleWidth).IsEqualTo(24);
        await Assert.That(layout.ChannelWidth).IsEqualTo(22);
    }

    [Test]
    public async Task Compute_VeryWideTerminal_TitleExpandsFully()
    {
        // 150 cols: Title = 150-56 = 94
        var layout = ColumnLayout.Compute(150);

        await Assert.That(layout.TitleWidth).IsEqualTo(94);
        await Assert.That(layout.ChannelWidth).IsEqualTo(22);
    }

    [Test]
    public async Task Compute_ZeroWidth_TitleReturnsMinimum()
    {
        var layout = ColumnLayout.Compute(0);

        await Assert.That(layout.TitleWidth).IsEqualTo(20);
        await Assert.That(layout.ChannelWidth).IsEqualTo(22);
    }

    [Test]
    public async Task Compute_FixedColumnsNeverChange()
    {
        foreach (var width in new[] { 0, 50, 80, 100, 150, 200 })
        {
            var layout = ColumnLayout.Compute(width);
            await Assert.That(layout.NumberWidth).IsEqualTo(4);
            await Assert.That(layout.ChannelWidth).IsEqualTo(22);
            await Assert.That(layout.AddedWidth).IsEqualTo(12);
            await Assert.That(layout.StatusWidth).IsEqualTo(10);
        }
    }
}
