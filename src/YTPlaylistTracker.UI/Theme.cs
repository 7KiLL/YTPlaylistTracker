namespace YTPlaylistTracker.UI;

public static class Theme
{
    // Scheme name constants for SchemeName-based lookups
    public const string SchemeFrame = "ytpt.Frame";
    public const string SchemeLink = "ytpt.Link";
    public const string SchemeSyncing = "ytpt.Syncing";
    public const string SchemeUpdateInstalled = "ytpt.UpdateInstalled";
    public const string SchemeUpdateAvailable = "ytpt.UpdateAvailable";
    public const string SchemeSectionHeader = "ytpt.SectionHeader";
    public const string SchemeDanger = "ytpt.Danger";
    public const string SchemeStatusActive = "ytpt.StatusActive";
    public const string SchemeStatusRemoved = "ytpt.StatusRemoved";
    public const string SchemeHintKey = "ytpt.HintKey";

    public static string CurrentName { get; private set; } = ThemePalette.Default.Name;

    public static void Apply(string? themeName = null)
    {
        var p = ThemePalette.ForName(themeName);
        CurrentName = p.Name;

        // Disable shadows globally — flat, clean look
        Dialog.DefaultShadow = ShadowStyles.None;
        Dialog.DefaultBorderStyle = LineStyle.Rounded;
        Button.DefaultShadow = ShadowStyles.None;

        Scheme OnBg(Color normal, Color focus, Color hotNormal, Color hotFocus) => new()
        {
            Normal = new Attribute(normal, p.Bg),
            Focus = new Attribute(p.Bg, focus),
            HotNormal = new Attribute(hotNormal, p.Bg),
            HotFocus = new Attribute(p.Bg, hotFocus),
            Disabled = new Attribute(p.FgMuted, p.Bg),
        };

        // Global Terminal.Gui schemes
        SchemeManager.AddScheme("Base", OnBg(p.Fg, p.Accent, p.Accent, p.Accent));
        SchemeManager.AddScheme("Dialog", OnBg(p.Fg, p.Accent, p.Accent, p.Accent));
        SchemeManager.AddScheme("Menu", OnBg(p.Accent, p.Accent, p.AccentBright, p.AccentBright));
        SchemeManager.AddScheme("Error", OnBg(p.Red, p.Red, p.RedBright, p.RedBright));

        // Custom app schemes
        SchemeManager.AddScheme(SchemeFrame, new Scheme
        {
            Normal = new Attribute(p.Accent, p.Bg),
            Focus = new Attribute(p.AccentBright, p.Bg),
            HotNormal = new Attribute(p.AccentBright, p.Bg),
            HotFocus = new Attribute(p.AccentBright, p.Bg),
            Disabled = new Attribute(p.FgMuted, p.Bg),
        });
        SchemeManager.AddScheme(SchemeLink, OnBg(p.AccentBright, p.Accent, p.AccentBright, p.Accent));
        SchemeManager.AddScheme(SchemeSyncing, OnBg(p.Yellow, p.Accent, p.Yellow, p.Accent));
        SchemeManager.AddScheme(SchemeUpdateInstalled, OnBg(p.Green, p.Accent, p.Green, p.Accent));
        SchemeManager.AddScheme(SchemeUpdateAvailable, OnBg(p.Cyan, p.Accent, p.Cyan, p.Accent));
        SchemeManager.AddScheme(SchemeSectionHeader, OnBg(p.AccentBright, p.Accent, p.AccentBright, p.Accent));
        SchemeManager.AddScheme(SchemeDanger, OnBg(p.RedBright, p.Red, p.RedBright, p.Red));
        SchemeManager.AddScheme(SchemeStatusActive, OnBg(p.Green, p.Accent, p.Green, p.Accent));
        SchemeManager.AddScheme(SchemeStatusRemoved, OnBg(p.RedBright, p.Red, p.RedBright, p.Red));
        SchemeManager.AddScheme(SchemeHintKey, OnBg(p.FgMuted, p.Accent, p.Accent, p.Accent));
    }
}
