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
            Table = dt
        };

        var closeBtn = new Button("Close", true);
        closeBtn.Clicked += () => global::Terminal.Gui.Application.RequestStop();

        Add(table);
        AddButton(closeBtn);
    }
}
