using System.Data;
using Terminal.Gui;
using YTPlaylistTracker.Domain.Entities;

namespace YTPlaylistTracker.UI.Views;

public sealed class RemovedVideosDialog : Dialog
{
    public RemovedVideosDialog(IReadOnlyList<Video> removedVideos, string playlistTitle)
        : base()
    {
        Title = $"Removed Videos - {playlistTitle}";
        Width = 80;
        Height = 25;
        ShadowStyle = ShadowStyle.None;
        BorderStyle = LineStyle.Rounded;
        var dt = new DataTable();
        dt.Columns.Add("#", typeof(int));
        dt.Columns.Add("Title", typeof(string));
        dt.Columns.Add("Channel", typeof(string));
        dt.Columns.Add("Reason", typeof(string));
        dt.Columns.Add("Removed At", typeof(string));

        var initialLayout = ColumnLayout.Compute(80);
        for (int i = 0; i < removedVideos.Count; i++)
        {
            var v = removedVideos[i];
            dt.Rows.Add(
                i + 1,
                UnicodeWidth.Truncate(v.Title ?? "", initialLayout.TitleWidth),
                UnicodeWidth.Truncate(v.ChannelTitle ?? "", initialLayout.ChannelWidth),
                v.RemovalReason?.ToString() ?? "Unknown",
                v.DeletedAt?.ToString("yyyy-MM-dd HH:mm") ?? "");
        }

        var table = new TableView
        {
            X = 0, Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
            FullRowSelect = true,
            Table = new DataTableSource(dt),
            Style = new TableStyle
            {
                ShowVerticalCellLines = false,
                ShowVerticalHeaderLines = false,
                ShowHorizontalHeaderOverline = false,
                ShowHorizontalHeaderUnderline = true,
                ExpandLastColumn = false,
                AlwaysShowHeaders = true,
                ColumnStyles = new Dictionary<int, ColumnStyle>(),
            },
        };

        var closeBtn = new Button() { ShadowStyle = ShadowStyle.None, Text = "Close", IsDefault = true };
        closeBtn.Accepting += (sender, e) => global::Terminal.Gui.Application.RequestStop();

        Add(table);

        table.DrawComplete += (sender, e) =>
        {
            if (table.Table == null) return;
            if (table.Viewport.Width <= 0) return;
            var layout = ColumnLayout.Compute(table.Viewport.Width);
            table.Style.ColumnStyles.Clear();
            table.Style.ColumnStyles[0] = new ColumnStyle
                { MinWidth = layout.NumberWidth, MaxWidth = layout.NumberWidth };
            table.Style.ColumnStyles[1] = new ColumnStyle
                { MinWidth = layout.TitleWidth };
            table.Style.ColumnStyles[2] = new ColumnStyle
                { MinWidth = layout.ChannelWidth, MaxWidth = layout.ChannelWidth };
            table.Style.ColumnStyles[3] = new ColumnStyle
            {
                MinWidth = 14, MaxWidth = 14,
                ColorGetter = args => Theme.StatusRemoved,
            };
            table.Style.ColumnStyles[4] = new ColumnStyle
                { MinWidth = 18, MaxWidth = 18 };
            table.SetNeedsDraw();
        };

        AddButton(closeBtn);
    }
}
