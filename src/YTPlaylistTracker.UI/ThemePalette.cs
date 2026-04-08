using Terminal.Gui;

namespace YTPlaylistTracker.UI;

/// <summary>
/// Base color palette mapped to ANSI 16-color positions.
/// Terminal emulator remaps these to its own RGB values.
///
/// Terminal.Gui v1 Color enum → ANSI index:
///   0=Black, 1=Blue, 2=Green, 3=Cyan, 4=Red, 5=Magenta, 6=Brown, 7=Gray,
///   8=DarkGray, 9=BrightBlue, 10=BrightGreen, 11=BrightCyan, 12=BrightRed,
///   13=BrightMagenta, 14=BrightYellow, 15=White
/// </summary>
public sealed record ThemePalette(
    string Name,
    Color Bg,
    Color BgSurface,
    Color Fg,
    Color FgMuted,
    Color Accent,
    Color AccentBright,
    Color Green,
    Color Red,
    Color RedBright,
    Color Yellow,
    Color Cyan)
{
    // ── Dark themes ──

    public static readonly ThemePalette Catppuccin = new(
        Name: "Catppuccin",
        Bg: Color.Black, BgSurface: Color.DarkGray,
        Fg: Color.White, FgMuted: Color.Gray,
        Accent: Color.Magenta, AccentBright: Color.BrightMagenta,
        Green: Color.Green, Red: Color.Red, RedBright: Color.BrightRed,
        Yellow: Color.BrightYellow, Cyan: Color.Cyan);

    public static readonly ThemePalette Dracula = new(
        Name: "Dracula",
        Bg: Color.Black, BgSurface: Color.DarkGray,
        Fg: Color.White, FgMuted: Color.Gray,
        Accent: Color.BrightMagenta, AccentBright: Color.BrightMagenta,
        Green: Color.BrightGreen, Red: Color.BrightRed, RedBright: Color.BrightRed,
        Yellow: Color.BrightYellow, Cyan: Color.BrightCyan);

    public static readonly ThemePalette GruvboxDark = new(
        Name: "Gruvbox Dark",
        Bg: Color.Black, BgSurface: Color.DarkGray,
        Fg: Color.BrightYellow, FgMuted: Color.Yellow,
        Accent: Color.Yellow, AccentBright: Color.BrightYellow,
        Green: Color.BrightGreen, Red: Color.Red, RedBright: Color.BrightRed,
        Yellow: Color.BrightYellow, Cyan: Color.Cyan);

    public static readonly ThemePalette Nord = new(
        Name: "Nord",
        Bg: Color.Black, BgSurface: Color.DarkGray,
        Fg: Color.White, FgMuted: Color.Gray,
        Accent: Color.Cyan, AccentBright: Color.BrightCyan,
        Green: Color.Green, Red: Color.Red, RedBright: Color.BrightRed,
        Yellow: Color.BrightYellow, Cyan: Color.BrightCyan);

    // ── Accessibility themes ──

    public static readonly ThemePalette HighContrastDark = new(
        Name: "High Contrast Dark",
        Bg: Color.Black, BgSurface: Color.DarkGray,
        Fg: Color.White, FgMuted: Color.Gray,
        Accent: Color.BrightCyan, AccentBright: Color.BrightCyan,
        Green: Color.BrightGreen, Red: Color.BrightRed, RedBright: Color.BrightRed,
        Yellow: Color.BrightYellow, Cyan: Color.BrightCyan);

    public static readonly ThemePalette HighContrastLight = new(
        Name: "High Contrast Light",
        Bg: Color.White, BgSurface: Color.Gray,
        Fg: Color.Black, FgMuted: Color.DarkGray,
        Accent: Color.Blue, AccentBright: Color.BrightBlue,
        Green: Color.Green, Red: Color.Red, RedBright: Color.BrightRed,
        Yellow: Color.Yellow, Cyan: Color.Blue);

    // ── Registry ──

    public static readonly ThemePalette[] All =
        [Catppuccin, Dracula, GruvboxDark, Nord, HighContrastDark, HighContrastLight];

    public static ThemePalette Default => Catppuccin;

    public static string[] AllNames => All.Select(t => t.Name).ToArray();

    public static ThemePalette ForName(string? name) =>
        All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? Default;
}
