using YTPlaylistTracker.Domain.Enums;
using YTPlaylistTracker.UI;

namespace YTPlaylistTracker.UnitTests.Helpers;

public class GlyphDetectorTests
{
    [Fact]
    public void NonWindows_ReturnsFullMode()
    {
        if (OperatingSystem.IsWindows()) return;

        var mode = GlyphDetector.Detect(_ => null);
        Assert.Equal(GlyphMode.Full, mode);
    }

    [Fact]
    public void NonWindows_IgnoresEnvVars()
    {
        if (OperatingSystem.IsWindows()) return;

        // Even with no env vars set, non-Windows is always Full
        var mode = GlyphDetector.Detect(_ => null);
        Assert.Equal(GlyphMode.Full, mode);
    }

    [Fact]
    public void Windows_NoModernTerminal_ReturnsBasic()
    {
        if (!OperatingSystem.IsWindows()) return;

        var mode = GlyphDetector.Detect(_ => null);
        Assert.Equal(GlyphMode.Basic, mode);
    }

    [Fact]
    public void Windows_WithWtSession_ReturnsFull()
    {
        if (!OperatingSystem.IsWindows()) return;

        var mode = GlyphDetector.Detect(key => key == "WT_SESSION" ? "some-guid" : null);
        Assert.Equal(GlyphMode.Full, mode);
    }

    [Fact]
    public void Windows_WithConEmu_ReturnsFull()
    {
        if (!OperatingSystem.IsWindows()) return;

        var mode = GlyphDetector.Detect(key => key == "ConEmuPID" ? "1234" : null);
        Assert.Equal(GlyphMode.Full, mode);
    }

    [Theory]
    [InlineData("mintty")]
    [InlineData("WezTerm")]
    [InlineData("Alacritty")]
    [InlineData("ghostty")]
    public void Windows_WithTermProgram_ReturnsFull(string termProgram)
    {
        if (!OperatingSystem.IsWindows()) return;

        var mode = GlyphDetector.Detect(key => key == "TERM_PROGRAM" ? termProgram : null);
        Assert.Equal(GlyphMode.Full, mode);
    }
}

public class GlyphsTests
{
    [Fact]
    public void SpinnerFrames_AreNonEmpty()
    {
        var frames = Glyphs.SpinnerFrames;
        Assert.NotEmpty(frames);
        Assert.All(frames, f => Assert.False(string.IsNullOrEmpty(f)));
    }

    [Fact]
    public void PlaylistIcon_RegularKind_ReturnsEmpty()
    {
        Assert.Equal("", Glyphs.PlaylistIcon(PlaylistKind.Regular));
    }

    [Theory]
    [InlineData(PlaylistKind.Liked)]
    [InlineData(PlaylistKind.WatchLater)]
    [InlineData(PlaylistKind.Uploads)]
    public void PlaylistIcon_SpecialKinds_ReturnNonEmpty(PlaylistKind kind)
    {
        Assert.False(string.IsNullOrEmpty(Glyphs.PlaylistIcon(kind)));
    }

    [Fact]
    public void SortArrows_AreNonEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(Glyphs.SortAscending));
        Assert.False(string.IsNullOrWhiteSpace(Glyphs.SortDescending));
    }

    [Fact]
    public void DefaultMarker_HasTrailingSpace()
    {
        Assert.EndsWith(" ", Glyphs.DefaultMarker);
    }

    [Fact]
    public void Check_IsNonEmpty()
    {
        Assert.False(string.IsNullOrEmpty(Glyphs.Check));
    }
}
