using Terminal.Gui;

namespace YTPlaylistTracker.UI.Views;

public class HelpDialog : Dialog
{
    public HelpDialog() : base("Keybindings", 52, 26)
    {
        (string, string)[] keys =
        [
            ("h / l / Left / Right", "Switch panes"),
            ("j / k / Up / Down", "Navigate within pane"),
            ("J / K / Shift+Up/Down", "Fast scroll (5 rows)"),
            ("Tab / Shift+Tab", "Cycle panes"),
            ("Enter", "View details"),
            ("/ / F3", "Search / filter videos"),
            ("o / F4", "Sort videos"),
            ("a / F1", "Add playlist by URL"),
            ("t / F2", "Toggle tracking"),
            ("T", "Track / untrack all"),
            ("s / F5", "Sync selected playlist"),
            ("S / F6", "Sync all tracked"),
            ("e / F7", "Export removed videos"),
            ("F8", "Toggle removed videos"),
            ("F9", "Settings"),
            ("H / F11", "Removal history"),
            ("? / F12", "This help"),
            ("u", "Check for updates"),
            ("q / F10", "Quit"),
            ("Ctrl+C (x2)", "Quit"),
        ];

        int y = 0;
        foreach (var (key, action) in keys)
        {
            Add(new Label(key) { X = 1, Y = y, ColorScheme = Colors.Menu });
            Add(new Label(action) { X = 26, Y = y });
            y++;
        }

        var closeBtn = new Button("Close", true);
        closeBtn.Clicked += () => global::Terminal.Gui.Application.RequestStop();
        AddButton(closeBtn);
    }
}
