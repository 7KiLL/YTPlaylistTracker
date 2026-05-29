
namespace YTPlaylistTracker.UI.Views;

public sealed class HelpDialog : Dialog
{
    private const int ColWidth = 42;
    private const int KeyCol = 2;
    private const int DescCol = 22;

    public HelpDialog() : base()
    {
        Title = "";
        Width = ColWidth * 2 + 5;
        Height = 22;
        Border!.Settings &= ~BorderSettings.Title;

        Add(new Label { Text = " Keybindings", X = 0, Y = 0, Width = Dim.Fill(), SchemeName = Theme.SchemeFrame });

        // Left column
        int xOff = 0;
        int y = 2;
        y = AddSection(xOff, y, "Navigation",
            ("h l \u2190 \u2192", "Switch panes"),
            ("j k \u2191 \u2193", "Navigate list"),
            ("J K Shift+\u2191/\u2193", "Fast scroll (5)"),
            ("Tab / Shift+Tab", "Cycle panes"),
            ("Enter", "Details / menu"));

        y = AddSection(xOff, y, "Profiles",
            ("n", "New profile"),
            ("L", "Login / Logout"),
            ("r", "Rename"),
            ("d", "Set as default"),
            ("x", "Delete"));

        // Right column
        xOff = ColWidth;
        y = 2;
        y = AddSection(xOff, y, "Playlists",
            ("a F1", "Add by URL"),
            ("t F2", "Toggle tracking"),
            ("T", "Track / untrack all"),
            ("s F5", "Sync selected"),
            ("S F6", "Sync all tracked"));

        y = AddSection(xOff, y, "Videos & App",
            ("/ F3", "Search / filter"),
            ("o F4", "Sort"),
            ("F8", "Show removed only"),
            ("e F7", "Export removed"),
            ("H F11", "Removal history"),
            ("F9 Ctrl+,", "Settings"),
            ("u", "Check for updates"),
            ("? F12", "This help"),
            ("q F10", "Quit"));

        var closeBtn = new Button { Text = "Close", IsDefault = true };
        closeBtn.Accepting += (sender, e) => App!.RequestStop();
        AddButton(closeBtn);
    }

    private int AddSection(int xOffset, int startY, string title, params (string key, string desc)[] bindings)
    {
        Add(new Label
        {
            Text = " " + title,
            X = xOffset + 1, Y = startY,
            Width = ColWidth - 2,
            SchemeName = Theme.SchemeSectionHeader,
        });

        int y = startY + 1;
        foreach (var (key, desc) in bindings)
        {
            Add(new Label { Text = key, X = xOffset + KeyCol, Y = y, SchemeName = Theme.SchemeHintKey });
            Add(new Label { Text = desc, X = xOffset + DescCol, Y = y });
            y++;
        }

        return y + 1;
    }
}
