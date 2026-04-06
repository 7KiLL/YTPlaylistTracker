using Terminal.Gui;

namespace YTPlaylistTracker.UI.Views;

public class HelpDialog : Dialog
{
    public HelpDialog() : base("Keybindings", 50, 20)
    {
        var keys = new[]
        {
            ("h / l / Left / Right", "Switch panes"),
            ("j / k / Up / Down", "Navigate within pane"),
            ("Tab / Shift+Tab", "Cycle panes"),
            ("Enter", "View details"),
            ("/", "Search / filter videos"),
            ("o", "Sort videos"),
            ("?", "This help"),
            ("a / F1", "Add playlist by URL"),
            ("t / F2", "Toggle tracking"),
            ("T", "Track / untrack all"),
            ("s / F5", "Sync selected playlist"),
            ("S / F6", "Sync all tracked"),
            ("F8", "Toggle removed videos"),
            ("F9", "Settings"),
            ("q / F10", "Quit"),
            ("Ctrl+C (x2)", "Quit"),
        };

        int y = 0;
        foreach (var (key, action) in keys)
        {
            Add(new Label(key) { X = 1, Y = y, ColorScheme = Colors.Menu });
            Add(new Label(action) { X = 24, Y = y });
            y++;
        }

        var closeBtn = new Button("Close", true);
        closeBtn.Clicked += () => global::Terminal.Gui.Application.RequestStop();
        AddButton(closeBtn);
    }
}
