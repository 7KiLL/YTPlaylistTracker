using Terminal.Gui;

namespace YTPlaylistTracker.UI.Views;

public sealed class HelpDialog : Dialog
{
    public HelpDialog() : base()
    {
        Title = "Keybindings";
        Width = 52;
        Height = 32;
        
        
        (string, string)[] keys =
        [
            ("── Navigation ──", ""),
            ("h / l / Left / Right", "Switch panes"),
            ("j / k / Up / Down", "Navigate within pane"),
            ("J / K / Shift+Up/Down", "Fast scroll (5 rows)"),
            ("Tab / Shift+Tab", "Cycle panes"),
            ("Enter", "Details / profile menu"),
            ("── Profiles (when focused) ──", ""),
            ("n", "New profile"),
            ("L", "Login / Logout"),
            ("r", "Rename profile"),
            ("d", "Set as default"),
            ("x", "Delete profile"),
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
            Add(new Label() { Text = key, X = 1, Y = y, ColorScheme = Theme.HintKey });
            Add(new Label() { Text = action, X = 26, Y = y });
            y++;
        }

        var closeBtn = new Button() { Text = "Close", IsDefault = true };
        closeBtn.Accepting += (sender, e) => global::Terminal.Gui.Application.RequestStop();
        AddButton(closeBtn);
    }
}
