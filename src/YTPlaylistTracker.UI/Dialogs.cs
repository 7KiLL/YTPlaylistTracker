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
            SchemeName = Theme.SchemeFrame,
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
                TGuiApp.RequestStop();
            };
            dialog.AddButton(btn);
        }

        TGuiApp.Run(dialog);
        return result;
    }

    internal static string? PromptForText(
        IRunnable host,
        string title,
        string okLabel,
        string? fieldLabel = null,
        string? description = null,
        string defaultValue = "",
        int width = 50,
        int height = 8)
    {
        var input = description is not null
            ? new TextField { Text = defaultValue, X = 1, Y = 3, Width = Dim.Fill(2) }
            : new TextField { Text = defaultValue, X = 8, Y = 1, Width = width - 14 };

        return host.Prompt<TextField, string?>(
            input,
            tf => tf.Text?.Trim(),
            null,
            p =>
            {
                p.Title = "";
                p.Width = width;
                p.Height = height;
                p.Border!.Settings &= ~BorderSettings.Title;
                p.Add(new Label
                {
                    Text = " " + title, X = 0, Y = 0,
                    Width = Dim.Fill(), SchemeName = Theme.SchemeFrame,
                });

                if (description is not null)
                    p.Add(new Label { Text = description, X = 1, Y = 2 });
                else if (fieldLabel is not null)
                    p.Add(new Label { Text = fieldLabel, X = 1, Y = 1 });

                var okBtn = p.Buttons?.FirstOrDefault(b => b.IsDefault);
                if (okBtn is not null) okBtn.Text = okLabel;
            });
    }
}
