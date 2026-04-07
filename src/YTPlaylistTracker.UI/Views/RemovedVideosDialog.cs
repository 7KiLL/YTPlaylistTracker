using System.Data;
using Terminal.Gui;
using YTPlaylistTracker.Domain.Entities;

namespace YTPlaylistTracker.UI.Views;

public class RemovedVideosDialog : Dialog
{
    public RemovedVideosDialog(List<Video> removedVideos, string playlistTitle)
        : base($"Removed Videos - {playlistTitle}", 80, 25)
    {
        var dt = new DataTable();
        dt.Columns.Add("#", typeof(int));
        dt.Columns.Add("Title", typeof(string));
        dt.Columns.Add("Channel", typeof(string));
        dt.Columns.Add("Reason", typeof(string));
        dt.Columns.Add("Removed At", typeof(string));

        for (int i = 0; i < removedVideos.Count; i++)
        {
            var v = removedVideos[i];
            dt.Rows.Add(
                i + 1,
                v.Title,
                v.ChannelTitle ?? "",
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
                ExpandLastColumn = true,
                AlwaysShowHeaders = true,
                ColumnStyles = new Dictionary<DataColumn, TableView.ColumnStyle>
                {
                    [dt.Columns[0]] = new() { MinWidth = 3, MaxWidth = 5 },
                    [dt.Columns[2]] = new() { MinWidth = 8, MaxWidth = 20 },
                    [dt.Columns[3]] = new() { MinWidth = 7, MaxWidth = 14 },
                    [dt.Columns[4]] = new() { MinWidth = 10, MaxWidth = 18 },
                }
            }
        };

        var closeBtn = new Button("Close", true);
        closeBtn.Clicked += () => global::Terminal.Gui.Application.RequestStop();

        Add(table);
        AddButton(closeBtn);
    }
}
