using Terminal.Gui;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Infrastructure.Platform;

namespace YTPlaylistTracker.UI.Views;

public sealed class DetailDialog : Dialog
{
    public DetailDialog(string title, ISystemLauncher? browser, params (string label, string value)[] fields)
        : base(title, 75, Math.Min(fields.Length + 5, 22))
    {
        string? url = null;
        int y = 0;
        foreach (var (label, value) in fields)
        {
            Add(new Label(label + ":") { X = 1, Y = y, ColorScheme = Colors.Menu });

            if (browser is not null && IsLink(label))
            {
                var linkUrl = value;
                var link = new Button(UnicodeWidth.Truncate(value, 50))
                {
                    X = 20, Y = y,
                    ColorScheme = Theme.Link,
                };
                link.Clicked += () => browser.OpenUrl(linkUrl);
                Add(link);
            }
            else
            {
                var valueLabel = new Label(UnicodeWidth.Truncate(value, 52)) { X = 20, Y = y };
                if (string.Equals(label, "Status", StringComparison.Ordinal))
                    valueLabel.ColorScheme = string.Equals(value, "Active", StringComparison.Ordinal) ? Theme.StatusActive : Theme.StatusRemoved;
                Add(valueLabel);
            }

            if (string.Equals(label, "URL", StringComparison.Ordinal)) url = value;
            y++;
        }

        if (browser is not null && url is not null)
        {
            var captured = url;
            var openBtn = new Button("Open in Browser");
            openBtn.Clicked += () => browser.OpenUrl(captured);
            AddButton(openBtn);
        }

        var closeBtn = new Button("Close", is_default: true);
        closeBtn.Clicked += () => global::Terminal.Gui.Application.RequestStop();
        AddButton(closeBtn);
    }

    private static bool IsLink(string label) =>
        label is "URL" or "Thumbnail";

    public static DetailDialog ForProfile(Profile profile, int playlistCount, int trackedCount, ISystemLauncher? browser = null)
    {
        return new DetailDialog("Profile Details", browser,
            ("Name", profile.Name),
            ("Channel", profile.ChannelTitle ?? "-"),
            ("Default", profile.IsDefault ? "Yes" : "No"),
            ("Channel ID", profile.YouTubeChannelId ?? "-"),
            ("Thumbnail", profile.ChannelThumbnailUrl ?? "-"),
            ("Created", profile.CreatedAt.ToString("yyyy-MM-dd HH:mm")),
            ("Playlists", playlistCount.ToString()),
            ("Tracked", trackedCount.ToString()));
    }

    public static DetailDialog ForPlaylist(Playlist playlist, int activeCount, int removedCount, ISystemLauncher? browser = null)
    {
        List<(string, string)> fields =
        [
            ("Title", playlist.Title ?? "-"),
            ("YouTube ID", playlist.YouTubePlaylistId),
            ("URL", "youtube.com/playlist?list=" + playlist.YouTubePlaylistId),
            ("Tracked", playlist.IsTracked ? "Yes" : "No"),
            ("Last Synced", playlist.LastSyncedAt?.ToString("yyyy-MM-dd HH:mm") ?? "Never"),
            ("Published", playlist.PublishedAt?.ToString("yyyy-MM-dd") ?? "-"),
            ("Thumbnail", playlist.ThumbnailUrl ?? "-"),
            ("Description", playlist.Description ?? "-"),
            ("Active Videos", activeCount.ToString()),
            ("Removed", removedCount.ToString()),
        ];

        return new DetailDialog("Playlist Details", browser, [.. fields]);
    }

    public static DetailDialog ForVideo(Video video, ISystemLauncher? browser = null)
    {
        List<(string, string)> fields =
        [
            ("Title", video.Title),
            ("Channel", video.ChannelTitle ?? "-"),
            ("YouTube ID", video.YouTubeVideoId),
            ("URL", "youtu.be/" + video.YouTubeVideoId),
            ("Thumbnail", video.ThumbnailUrl ?? "i.ytimg.com/vi/" + video.YouTubeVideoId + "/mqdefault.jpg"),
            ("Added", video.AddedAt?.ToString("yyyy-MM-dd HH:mm") ?? "-"),
        ];

        if (video.DeletedAt.HasValue)
        {
            fields.Add(("Status", "Removed"));
            fields.Add(("Removed At", video.DeletedAt.Value.ToString("yyyy-MM-dd HH:mm")));
            fields.Add(("Reason", video.RemovalReason?.ToString() ?? "Unknown"));
        }
        else
        {
            fields.Add(("Status", "Active"));
        }

        fields.Add(("Description", video.Description ?? "-"));

        return new DetailDialog("Video Details", browser, [.. fields]);
    }
}
