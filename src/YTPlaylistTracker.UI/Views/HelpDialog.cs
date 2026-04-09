using Terminal.Gui;

namespace YTPlaylistTracker.UI.Views;

public sealed class HelpDialog : Dialog
{
    public HelpDialog() : base()
    {
        Title = "";
        Width = 62;
        Height = 35;
        Border!.Settings &= ~BorderSettings.Title;

        Add(new Label { Text = " Keybindings", X = 0, Y = 0, Width = Dim.Fill(), ColorScheme = Theme.Frame });

        int y = 2;

        y = AddSection("Navigation", y,
            ("h  l  \u2190  \u2192", "Switch panes"),
            ("j  k  \u2191  \u2193", "Navigate list"),
            ("J  K  Shift+\u2191/\u2193", "Fast scroll (5 rows)"),
            ("Tab  Shift+Tab", "Cycle panes"),
            ("Enter", "Open details / profile menu"));

        y = AddSection("Profiles", y,
            ("n", "New profile"),
            ("L", "Login / Logout"),
            ("r", "Rename"),
            ("d", "Set as default"),
            ("x", "Delete"));

        y = AddSection("Playlists", y,
            ("a  F1", "Add by URL"),
            ("t  F2", "Toggle tracking"),
            ("T", "Track / untrack all"),
            ("s  F5", "Sync selected"),
            ("S  F6", "Sync all tracked"));

        y = AddSection("Videos", y,
            ("/  F3", "Search / filter"),
            ("o  F4", "Sort"),
            ("F8", "Show removed only"),
            ("e  F7", "Export removed"),
            ("H  F11", "Removal history"));

        AddSection("App", y,
            ("F9  Ctrl+,", "Settings"),
            ("u", "Check for updates"),
            ("?  F12", "This help"),
            ("q  F10", "Quit"),
            ("Ctrl+C (x2)", "Force quit"));

        var closeBtn = new Button() { Text = "Close", IsDefault = true };
        closeBtn.Accepting += (sender, e) => global::Terminal.Gui.Application.RequestStop();
        AddButton(closeBtn);
    }

    private int AddSection(string title, int startY, params (string key, string desc)[] bindings)
    {
        Add(new Label
        {
            Text = " " + title,
            X = 1, Y = startY,
            Width = Dim.Fill(1),
            ColorScheme = Theme.SectionHeader,
        });

        int y = startY + 1;
        foreach (var (key, desc) in bindings)
        {
            Add(new Label { Text = "  " + key, X = 1, Y = y, ColorScheme = Theme.HintKey });
            Add(new Label { Text = desc, X = 30, Y = y });
            y++;
        }

        return y + 1; // gap between sections
    }
}
