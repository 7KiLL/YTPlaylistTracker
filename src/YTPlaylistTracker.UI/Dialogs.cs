using Terminal.Gui;

namespace YTPlaylistTracker.UI;

/// <summary>
/// Replacement for MessageBox.Query that uses interior title labels
/// instead of border-embedded titles with ┤├ connectors.
/// </summary>
internal static class Dialogs
{
    internal static int Query(string title, string message, params string[] buttons)
    {
        int result = -1;
        var lines = message.Split('\n');
        var maxLineWidth = lines.Max(l => l.Length);
        var width = Math.Max(Math.Max(maxLineWidth + 6, title.Length + 6), 30);
        var height = lines.Length + 7; // title label + spacing + message + spacing + buttons + border

        var dialog = new Dialog { Title = "", Width = width, Height = height };
        dialog.Border!.Settings &= ~BorderSettings.Title;

        dialog.Add(new Label
        {
            Text = " " + title,
            X = 0, Y = 0,
            Width = Dim.Fill(),
            ColorScheme = Theme.Frame,
        });

        dialog.Add(new Label
        {
            Text = message,
            X = 1, Y = 2,
            Width = Dim.Fill(1),
            Height = lines.Length,
        });

        for (var i = 0; i < buttons.Length; i++)
        {
            var idx = i;
            var btn = new Button { Text = buttons[i], IsDefault = i == 0 };
            btn.Accepting += (s, e) =>
            {
                result = idx;
                global::Terminal.Gui.Application.RequestStop();
            };
            dialog.AddButton(btn);
        }

        global::Terminal.Gui.Application.Run(dialog);
        return result;
    }
}
