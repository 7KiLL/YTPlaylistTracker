using System.Data;
using Terminal.Gui;
using YTPlaylistTracker.Domain.Entities;

namespace YTPlaylistTracker.UI.Views;

public sealed class RemovalHistoryDialog : Dialog
{
    public RemovalHistoryDialog(IReadOnlyList<(Playlist Playlist, Video Video)> removedVideos)
        : base("Removal History - All Playlists", 90, 28)
    {
        var dt = new DataTable();
        dt.Columns.Add("Date", typeof(string));
        dt.Columns.Add("Playlist", typeof(string));
        dt.Columns.Add("Title", typeof(string));
        dt.Columns.Add("Channel", typeof(string));
        dt.Columns.Add("Reason", typeof(string));

        // Group by date
        string? lastDate = null;
        foreach (var (playlist, video) in removedVideos)
        {
            var dateStr = video.DeletedAt?.ToString("yyyy-MM-dd") ?? "";
            var displayDate = dateStr == lastDate ? "" : dateStr;
            lastDate = dateStr;

            dt.Rows.Add(
                displayDate,
                UnicodeWidth.Truncate(playlist.Title ?? playlist.YouTubePlaylistId, 20),
                UnicodeWidth.Truncate(video.Title, 30),
                UnicodeWidth.Truncate(video.ChannelTitle ?? "", 15),
                video.RemovalReason?.ToString() ?? "Unknown");
        }

        if (removedVideos.Count == 0)
        {
            dt.Rows.Add("", "", "No removed videos found.", "", "");
        }

        var countLabel = new Label($"{removedVideos.Count} removed videos across all tracked playlists")
        {
            X = 0, Y = 0, Width = Dim.Fill()
        };

        var table = new TableView
        {
            X = 0, Y = 1,
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
                ColumnStyles = new Dictionary<System.Data.DataColumn, TableView.ColumnStyle>
                {
                    [dt.Columns["Reason"]!] = new()
                    {
                        ColorGetter = args =>
                        {
                            var val = args.CellValue?.ToString() ?? "";
                            return val is "Deleted" or "Private" or "Unknown"
                                ? Theme.StatusRemoved
                                : null;
                        }
                    }
                }
            }
        };

        var closeBtn = new Button("Close", true);
        closeBtn.Clicked += () => global::Terminal.Gui.Application.RequestStop();

        Add(countLabel, table);
        AddButton(closeBtn);
    }

}
