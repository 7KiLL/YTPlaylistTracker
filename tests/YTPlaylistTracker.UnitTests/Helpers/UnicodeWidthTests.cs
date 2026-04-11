using YTPlaylistTracker.UI;

namespace YTPlaylistTracker.UnitTests.Helpers;

public class UnicodeWidthTests
{
    [Test]
    public async Task GetWidth_AsciiOnly_EqualsLength()
    {
        await Assert.That(UnicodeWidth.GetWidth("hello")).IsEqualTo(5);
    }

    [Test]
    public async Task GetWidth_CjkCharacters_DoubleCounted()
    {
        // Each CJK char = 2 terminal columns
        await Assert.That(UnicodeWidth.GetWidth("\u591c\u7a93")).IsEqualTo(4);
    }

    [Test]
    public async Task GetWidth_MixedAsciiAndCjk()
    {
        // "cephalo - " = 10, "\u591c\u7a93" = 4 -> total 14
        await Assert.That(UnicodeWidth.GetWidth("cephalo - \u591c\u7a93")).IsEqualTo(14);
    }

    [Test]
    public async Task GetWidth_EmptyString_ReturnsZero()
    {
        await Assert.That(UnicodeWidth.GetWidth("")).IsEqualTo(0);
    }

    [Test]
    public async Task Truncate_FitsWithinLimit_ReturnsOriginal()
    {
        await Assert.That(UnicodeWidth.Truncate("hello", 10)).IsEqualTo("hello");
    }

    [Test]
    public async Task Truncate_CjkFitsExactly_ReturnsOriginal()
    {
        // "\u591c\u7a93" = 4 display columns, limit 4
        await Assert.That(UnicodeWidth.Truncate("\u591c\u7a93", 4)).IsEqualTo("\u591c\u7a93");
    }

    [Test]
    public async Task Truncate_CjkExceedsLimit_Truncates()
    {
        // "\u591c\u7a93\u660e" = 6 display columns, limit 5 -> must truncate
        var result = UnicodeWidth.Truncate("\u591c\u7a93\u660e", 5);
        await Assert.That(result).EndsWith("..");
        await Assert.That(UnicodeWidth.GetWidth(result) <= 5).IsTrue();
    }

    [Test]
    public async Task Truncate_MixedString_TruncatesCorrectly()
    {
        // "cephalo - \u591c\u7a93" = 14 display columns
        var full = "cephalo - \u591c\u7a93";
        await Assert.That(UnicodeWidth.Truncate(full, 14)).IsEqualTo(full);

        var truncated = UnicodeWidth.Truncate(full, 13);
        await Assert.That(truncated).EndsWith("..");
        await Assert.That(UnicodeWidth.GetWidth(truncated) <= 13).IsTrue();
    }

    [Test]
    public async Task Truncate_NullOrEmpty_ReturnsAsIs()
    {
        await Assert.That(UnicodeWidth.Truncate(null!, 10)).IsNull();
        await Assert.That(UnicodeWidth.Truncate("", 10)).IsEqualTo("");
    }

    [Test]
    public async Task Truncate_AsciiExceedsLimit_Truncates()
    {
        var result = UnicodeWidth.Truncate("abcdefghij", 7);
        await Assert.That(result).EndsWith("..");
        await Assert.That(UnicodeWidth.GetWidth(result) <= 7).IsTrue();
    }
}
