using Terminal.Gui;

namespace YTPlaylistTracker.UI;

/// <summary>
/// TrueColor (24-bit RGB) theme palettes.
/// Terminal.Gui v2 renders these directly; no ANSI 16-color approximation.
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

    // Catppuccin Mocha — https://catppuccin.com/palette
    public static readonly ThemePalette Catppuccin = new(
        Name: "Catppuccin",
        Bg: new("#1e1e2e"), BgSurface: new("#313244"),
        Fg: new("#cdd6f4"), FgMuted: new("#6c7086"),
        Accent: new("#cba6f7"), AccentBright: new("#f5c2e7"),
        Green: new("#a6e3a1"), Red: new("#f38ba8"), RedBright: new("#eba0ac"),
        Yellow: new("#f9e2af"), Cyan: new("#89dceb"));

    // Dracula — https://draculatheme.com/contribute
    public static readonly ThemePalette Dracula = new(
        Name: "Dracula",
        Bg: new("#282a36"), BgSurface: new("#44475a"),
        Fg: new("#f8f8f2"), FgMuted: new("#6272a4"),
        Accent: new("#bd93f9"), AccentBright: new("#ff79c6"),
        Green: new("#50fa7b"), Red: new("#ff5555"), RedBright: new("#ff6e6e"),
        Yellow: new("#f1fa8c"), Cyan: new("#8be9fd"));

    // Gruvbox Dark — https://github.com/morhetz/gruvbox
    public static readonly ThemePalette GruvboxDark = new(
        Name: "Gruvbox Dark",
        Bg: new("#282828"), BgSurface: new("#3c3836"),
        Fg: new("#ebdbb2"), FgMuted: new("#928374"),
        Accent: new("#d79921"), AccentBright: new("#fabd2f"),
        Green: new("#b8bb26"), Red: new("#cc241d"), RedBright: new("#fb4934"),
        Yellow: new("#fabd2f"), Cyan: new("#83a598"));

    // Nord — https://www.nordtheme.com/docs/colors-and-palettes
    public static readonly ThemePalette Nord = new(
        Name: "Nord",
        Bg: new("#2e3440"), BgSurface: new("#3b4252"),
        Fg: new("#eceff4"), FgMuted: new("#4c566a"),
        Accent: new("#88c0d0"), AccentBright: new("#8fbcbb"),
        Green: new("#a3be8c"), Red: new("#bf616a"), RedBright: new("#d08770"),
        Yellow: new("#ebcb8b"), Cyan: new("#81a1c1"));

    // ── Accessibility themes ──

    public static readonly ThemePalette HighContrastDark = new(
        Name: "High Contrast Dark",
        Bg: new("#000000"), BgSurface: new("#1a1a1a"),
        Fg: new("#ffffff"), FgMuted: new("#999999"),
        Accent: new("#00ffff"), AccentBright: new("#00ffff"),
        Green: new("#00ff00"), Red: new("#ff4444"), RedBright: new("#ff4444"),
        Yellow: new("#ffff00"), Cyan: new("#00ffff"));

    public static readonly ThemePalette HighContrastLight = new(
        Name: "High Contrast Light",
        Bg: new("#ffffff"), BgSurface: new("#e0e0e0"),
        Fg: new("#000000"), FgMuted: new("#555555"),
        Accent: new("#0055aa"), AccentBright: new("#0077cc"),
        Green: new("#008800"), Red: new("#cc0000"), RedBright: new("#dd2222"),
        Yellow: new("#886600"), Cyan: new("#005599"));

    // ── Registry ──

    public static readonly ThemePalette[] All =
        [Catppuccin, Dracula, GruvboxDark, Nord, HighContrastDark, HighContrastLight];

    public static ThemePalette Default => Catppuccin;

    public static string[] AllNames => All.Select(t => t.Name).ToArray();

    public static ThemePalette ForName(string? name) =>
        All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? Default;
}
