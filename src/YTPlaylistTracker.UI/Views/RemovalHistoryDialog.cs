using System.Data;
using Terminal.Gui;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Infrastructure.Platform;

namespace YTPlaylistTracker.UI.Views;

public sealed class RemovalHistoryDialog : Dialog
{
    private readonly IReadOnlyList<(Playlist Playlist, Video Video)> _removedVideos;
    private readonly ISystemLauncher? _browser;

    public RemovalHistoryDialog(IReadOnlyList<(Playlist Playlist, Video Video)> removedVideos, ISystemLauncher? browser = null)
        : base()
    {
        _removedVideos = removedVideos;
        _browser = browser;
        Title = "Removal History - All Playlists";
        Width = 90;
        Height = 28;

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
            var displayDate = string.Equals(dateStr, lastDate, StringComparison.Ordinal) ? "" : dateStr;
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

        var countLabel = new Label()
        {
            Text = $"{removedVideos.Count} removed videos across all tracked playlists  │  Enter: details",
            X = 0, Y = 0, Width = Dim.Fill(),
        };

        var table = new TableView
        {
            X = 0, Y = 1,
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
                ExpandLastColumn = true,
                AlwaysShowHeaders = true,
                ColumnStyles = new Dictionary<int, ColumnStyle>
                {
                    [4] = new()
                    {
                        ColorGetter = args =>
                        {
                            var val = args.CellValue?.ToString() ?? "";
                            return val is "Deleted" or "Private" or "Unknown"
                                ? Theme.StatusRemoved
                                : null;
                        },
                    },
                },
            },
        };

        table.CellActivated += (sender, e) => ShowVideoDetail(table.SelectedRow);

        var closeBtn = new Button() { Text = "Close", IsDefault = true };
        closeBtn.Accepting += (sender, e) => global::Terminal.Gui.Application.RequestStop();

        Add(countLabel, table);
        AddButton(closeBtn);
    }

    private void ShowVideoDetail(int row)
    {
        if (row < 0 || row >= _removedVideos.Count) return;
        var (_, video) = _removedVideos[row];
        var dialog = DetailDialog.ForVideo(video, _browser);
        global::Terminal.Gui.Application.Run(dialog);
    }
}
