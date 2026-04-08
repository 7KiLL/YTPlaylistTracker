using System.Data;
using Terminal.Gui;
using YTPlaylistTracker.Domain.Entities;

namespace YTPlaylistTracker.UI.Views;

public sealed class RemovedVideosDialog : Dialog
{
    public RemovedVideosDialog(IReadOnlyList<Video> removedVideos, string playlistTitle)
        : base($"Removed Videos - {playlistTitle}", 80, 25)
    {
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
            Table = dt,
            Style = new TableView.TableStyle
            {
                ShowVerticalCellLines = false,
                ShowVerticalHeaderLines = false,
                ShowHorizontalHeaderOverline = false,
                ShowHorizontalHeaderUnderline = true,
                ExpandLastColumn = false,
                AlwaysShowHeaders = true,
                ColumnStyles = new Dictionary<DataColumn, TableView.ColumnStyle>()
            }
        };

        var closeBtn = new Button("Close", true);
        closeBtn.Clicked += () => global::Terminal.Gui.Application.RequestStop();

        Add(table);

        table.LayoutComplete += (_) =>
        {
            if (table.Table == null) return;
            var layout = ColumnLayout.Compute(table.Bounds.Width);
            table.Style.ColumnStyles.Clear();
            table.Style.ColumnStyles[dt.Columns[0]] = new TableView.ColumnStyle
                { MinWidth = layout.NumberWidth, MaxWidth = layout.NumberWidth };
            table.Style.ColumnStyles[dt.Columns[1]] = new TableView.ColumnStyle
                { MinWidth = layout.TitleWidth };
            table.Style.ColumnStyles[dt.Columns[2]] = new TableView.ColumnStyle
                { MinWidth = layout.ChannelWidth, MaxWidth = layout.ChannelWidth };
            table.Style.ColumnStyles[dt.Columns[3]] = new TableView.ColumnStyle
            {
                MinWidth = 14, MaxWidth = 14,
                ColorGetter = args => Theme.StatusRemoved
            };
            table.Style.ColumnStyles[dt.Columns[4]] = new TableView.ColumnStyle
                { MinWidth = 18, MaxWidth = 18 };
            table.SetNeedsDisplay();
        };

        AddButton(closeBtn);
    }
}
