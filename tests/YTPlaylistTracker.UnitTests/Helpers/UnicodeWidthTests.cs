using YTPlaylistTracker.UI;

namespace YTPlaylistTracker.UnitTests.Helpers;

public class UnicodeWidthTests
{
    [Fact]
    public void GetWidth_AsciiOnly_EqualsLength()
    {
        Assert.Equal(5, UnicodeWidth.GetWidth("hello"));
    }

    [Fact]
    public void GetWidth_CjkCharacters_DoubleCounted()
    {
        // Each CJK char = 2 terminal columns
        Assert.Equal(4, UnicodeWidth.GetWidth("夜窓"));
    }

    [Fact]
    public void GetWidth_MixedAsciiAndCjk()
    {
        // "cephalo - " = 10, "夜窓" = 4 → total 14
        Assert.Equal(14, UnicodeWidth.GetWidth("cephalo - 夜窓"));
    }

    [Fact]
    public void GetWidth_EmptyString_ReturnsZero()
    {
        Assert.Equal(0, UnicodeWidth.GetWidth(""));
    }

    [Fact]
    public void Truncate_FitsWithinLimit_ReturnsOriginal()
    {
        Assert.Equal("hello", UnicodeWidth.Truncate("hello", 10));
    }

    [Fact]
    public void Truncate_CjkFitsExactly_ReturnsOriginal()
    {
        // "夜窓" = 4 display columns, limit 4
        Assert.Equal("夜窓", UnicodeWidth.Truncate("夜窓", 4));
    }

    [Fact]
    public void Truncate_CjkExceedsLimit_Truncates()
    {
        // "夜窓明" = 6 display columns, limit 5 → must truncate
        var result = UnicodeWidth.Truncate("夜窓明", 5);
        Assert.EndsWith("..", result);
        Assert.True(UnicodeWidth.GetWidth(result) <= 5);
    }

    [Fact]
    public void Truncate_MixedString_TruncatesCorrectly()
    {
        // "cephalo - 夜窓" = 14 display columns
        var full = "cephalo - 夜窓";
        Assert.Equal(full, UnicodeWidth.Truncate(full, 14));

        var truncated = UnicodeWidth.Truncate(full, 13);
        Assert.EndsWith("..", truncated);
        Assert.True(UnicodeWidth.GetWidth(truncated) <= 13);
    }

    [Fact]
    public void Truncate_NullOrEmpty_ReturnsAsIs()
    {
        Assert.Null(UnicodeWidth.Truncate(null!, 10));
        Assert.Equal("", UnicodeWidth.Truncate("", 10));
    }

    [Fact]
    public void Truncate_AsciiExceedsLimit_Truncates()
    {
        var result = UnicodeWidth.Truncate("abcdefghij", 7);
        Assert.EndsWith("..", result);
        Assert.True(UnicodeWidth.GetWidth(result) <= 7);
    }
}
