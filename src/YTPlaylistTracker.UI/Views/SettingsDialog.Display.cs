using Terminal.Gui;
using YTPlaylistTracker.Domain.Interfaces;

namespace YTPlaylistTracker.UI.Views;

public sealed partial class SettingsDialog
{
    private static View BuildDisplayTab(IUserSettings userSettings)
    {
        var view = new View() { Width = Dim.Fill(), Height = Dim.Fill(), CanFocus = true };
        int y = 0;

        // ── Icons ──
        view.Add(new Label() { Text = "── Icons ───────────────────────────────────────────────────", X = 1, Y = y, ColorScheme = Theme.SectionHeader });
        y += 1;

        view.Add(new Label() { Text = "  Mode:", X = 1, Y = y });
        var glyphLabels = new[] { "Auto-detect", "Full (emoji/braille)", "Basic (ASCII)" };
        var glyphValues = new[] { "", "full", "basic" };
        var glyphIdx = Array.IndexOf(glyphValues, userSettings.GlyphMode);
        if (glyphIdx < 0) glyphIdx = 0;
        var glyphRadio = new RadioGroup()
        {
            RadioLabels = glyphLabels,
            X = 12, Y = y,
            SelectedItem = glyphIdx,
        };
        glyphRadio.SelectedItemChanged += (sender, e) =>
        {
            userSettings.GlyphMode = glyphValues[glyphRadio.SelectedItem];
            userSettings.Save();
            GlyphDetector.SetUserOverride(userSettings.GlyphMode);
        };
        view.Add(glyphRadio);
        y += glyphLabels.Length + 1;

        view.Add(new Label()
        {
            Text = "  Auto-detect picks Full on modern terminals (Windows Terminal,\n"
                 + "  Alacritty, etc.) and Basic on classic Windows console.\n\n"
                 + "  Full mode requires a font with emoji and CJK glyph support\n"
                 + "  (e.g. Nerd Font, Cascadia Code, Fira Code).",
            X = 1, Y = y,
            ColorScheme = Colors.ColorSchemes["Menu"],
        });

        return view;
    }
}
