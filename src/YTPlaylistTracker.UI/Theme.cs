using Terminal.Gui;
using App = Terminal.Gui.Application;

namespace YTPlaylistTracker.UI;

public static class Theme
{
    // ── Window title states ──
    public static ColorScheme Syncing { get; private set; } = null!;
    public static ColorScheme UpdateInstalled { get; private set; } = null!;
    public static ColorScheme UpdateAvailable { get; private set; } = null!;

    // ── Structural ──
    public static ColorScheme Frame { get; private set; } = null!;
    public static ColorScheme Link { get; private set; } = null!;

    // ── UI accents ──
    public static ColorScheme SectionHeader { get; private set; } = null!;
    public static ColorScheme Danger { get; private set; } = null!;
    public static ColorScheme StatusActive { get; private set; } = null!;
    public static ColorScheme StatusRemoved { get; private set; } = null!;
    public static ColorScheme HintKey { get; private set; } = null!;

    public static string CurrentName { get; private set; } = ThemePalette.Default.Name;

    public static void Apply(string? themeName = null)
    {
        var p = ThemePalette.ForName(themeName);
        CurrentName = p.Name;

        // Disable shadows globally — flat, clean look
        Dialog.DefaultShadow = ShadowStyle.None;
        Dialog.DefaultBorderStyle = LineStyle.Rounded;
        Button.DefaultShadow = ShadowStyle.None;
        MessageBox.DefaultBorderStyle = LineStyle.Rounded;

        ColorScheme OnBg(Color normal, Color focus, Color hotNormal, Color hotFocus) => new()
        {
            Normal = new Terminal.Gui.Attribute(normal, p.Bg),
            Focus = new Terminal.Gui.Attribute(p.Bg, focus),
            HotNormal = new Terminal.Gui.Attribute(hotNormal, p.Bg),
            HotFocus = new Terminal.Gui.Attribute(p.Bg, hotFocus),
            Disabled = new Terminal.Gui.Attribute(p.FgMuted, p.Bg),
        };

        // Global Terminal.Gui schemes
        Colors.ColorSchemes["Base"] = OnBg(p.Fg, p.Accent, p.Accent, p.Accent);
        Colors.ColorSchemes["Dialog"] = OnBg(p.Fg, p.Accent, p.Accent, p.Accent);
        Colors.ColorSchemes["Menu"] = OnBg(p.Accent, p.Accent, p.AccentBright, p.AccentBright);
        Colors.ColorSchemes["Error"] = OnBg(p.Red, p.Red, p.RedBright, p.RedBright);

        // Frame borders & titles — accent text, no inversion on focus
        Frame = new ColorScheme
        {
            Normal = new Terminal.Gui.Attribute(p.Accent, p.Bg),
            Focus = new Terminal.Gui.Attribute(p.AccentBright, p.Bg),
            HotNormal = new Terminal.Gui.Attribute(p.AccentBright, p.Bg),
            HotFocus = new Terminal.Gui.Attribute(p.AccentBright, p.Bg),
            Disabled = new Terminal.Gui.Attribute(p.FgMuted, p.Bg),
        };

        // Clickable links — accent-colored to match theme
        Link = OnBg(p.AccentBright, p.Accent, p.AccentBright, p.Accent);

        // Window title states
        Syncing = OnBg(p.Yellow, p.Accent, p.Yellow, p.Accent);
        UpdateInstalled = OnBg(p.Green, p.Accent, p.Green, p.Accent);
        UpdateAvailable = OnBg(p.Cyan, p.Accent, p.Cyan, p.Accent);

        // Settings section headers
        SectionHeader = OnBg(p.AccentBright, p.Accent, p.AccentBright, p.Accent);

        // Destructive actions
        Danger = OnBg(p.RedBright, p.Red, p.RedBright, p.Red);

        // Video status colors
        StatusActive = OnBg(p.Green, p.Accent, p.Green, p.Accent);
        StatusRemoved = OnBg(p.RedBright, p.Red, p.RedBright, p.Red);

        // Hint bar — same bg, muted text
        HintKey = OnBg(p.FgMuted, p.Accent, p.Accent, p.Accent);
    }
}
