using YTPlaylistTracker.Domain.Enums;
using YTPlaylistTracker.UI;

namespace YTPlaylistTracker.UnitTests.Helpers;

public class GlyphDetectorTests
{
    [Test]
    public async Task NonWindows_ReturnsFullMode()
    {
        if (OperatingSystem.IsWindows()) return;

        var mode = GlyphDetector.Detect(_ => null);
        await Assert.That(mode).IsEqualTo(GlyphMode.Full);
    }

    [Test]
    public async Task NonWindows_IgnoresEnvVars()
    {
        if (OperatingSystem.IsWindows()) return;

        // Even with no env vars set, non-Windows is always Full
        var mode = GlyphDetector.Detect(_ => null);
        await Assert.That(mode).IsEqualTo(GlyphMode.Full);
    }

    [Test]
    public async Task Windows_NoModernTerminal_ReturnsBasic()
    {
        if (!OperatingSystem.IsWindows()) return;

        var mode = GlyphDetector.Detect(_ => null);
        await Assert.That(mode).IsEqualTo(GlyphMode.Basic);
    }

    [Test]
    public async Task Windows_WithWtSession_ReturnsFull()
    {
        if (!OperatingSystem.IsWindows()) return;

        var mode = GlyphDetector.Detect(key => key == "WT_SESSION" ? "some-guid" : null);
        await Assert.That(mode).IsEqualTo(GlyphMode.Full);
    }

    [Test]
    public async Task Windows_WithConEmu_ReturnsFull()
    {
        if (!OperatingSystem.IsWindows()) return;

        var mode = GlyphDetector.Detect(key => key == "ConEmuPID" ? "1234" : null);
        await Assert.That(mode).IsEqualTo(GlyphMode.Full);
    }

    [Test]
    [Arguments("mintty")]
    [Arguments("WezTerm")]
    [Arguments("Alacritty")]
    [Arguments("ghostty")]
    public async Task Windows_WithTermProgram_ReturnsFull(string termProgram)
    {
        if (!OperatingSystem.IsWindows()) return;

        var mode = GlyphDetector.Detect(key => key == "TERM_PROGRAM" ? termProgram : null);
        await Assert.That(mode).IsEqualTo(GlyphMode.Full);
    }
}

public class GlyphsTests
{
    [Test]
    public async Task SpinnerFrames_AreNonEmpty()
    {
        var frames = Glyphs.SpinnerFrames;
        await Assert.That(frames).IsNotEmpty();
        foreach (var f in frames)
        {
            await Assert.That(string.IsNullOrEmpty(f)).IsFalse();
        }
    }

    [Test]
    public async Task PlaylistIcon_RegularKind_ReturnsEmpty()
    {
        await Assert.That(Glyphs.PlaylistIcon(PlaylistKind.Regular)).IsEqualTo("");
    }

    [Test]
    [Arguments(PlaylistKind.Liked)]
    [Arguments(PlaylistKind.WatchLater)]
    [Arguments(PlaylistKind.Uploads)]
    public async Task PlaylistIcon_SpecialKinds_ReturnNonEmpty(PlaylistKind kind)
    {
        await Assert.That(string.IsNullOrEmpty(Glyphs.PlaylistIcon(kind))).IsFalse();
    }

    [Test]
    public async Task SortArrows_AreNonEmpty()
    {
        await Assert.That(string.IsNullOrWhiteSpace(Glyphs.SortAscending)).IsFalse();
        await Assert.That(string.IsNullOrWhiteSpace(Glyphs.SortDescending)).IsFalse();
    }

    [Test]
    public async Task DefaultMarker_HasTrailingSpace()
    {
        await Assert.That(Glyphs.DefaultMarker).EndsWith(" ");
    }

    [Test]
    public async Task Check_IsNonEmpty()
    {
        await Assert.That(string.IsNullOrEmpty(Glyphs.Check)).IsFalse();
    }
}
