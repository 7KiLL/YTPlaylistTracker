using Terminal.Gui;
using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Infrastructure.Platform;

namespace YTPlaylistTracker.UI.Views;

public sealed class DetailDialog : Dialog
{
    public DetailDialog(string title, ISystemLauncher? browser, params (string label, string value)[] fields)
        : base()
    {
        Title = "";
        Width = 75;
        BorderStyle = LineStyle.Rounded;
        Border!.Settings &= ~BorderSettings.Title;

        Add(new Label { Text = " " + title, X = 0, Y = 0, Width = Dim.Fill(), ColorScheme = Theme.Frame });

        // Separate description from other fields — it gets a scrollable area
        var normalFields = fields.Where(f => !string.Equals(f.label, "Description", StringComparison.Ordinal)).ToArray();
        var description = fields.FirstOrDefault(f => string.Equals(f.label, "Description", StringComparison.Ordinal)).value;
        bool hasDescription = description is not null and not "-" and not "";

        // Height: fields + description area + chrome + interior title
        int descHeight = hasDescription ? 6 : 0;
        Height = Math.Min(normalFields.Length + descHeight + 6, 27);

        string? url = null;
        int y = 1;
        foreach (var (label, value) in normalFields)
        {
            Add(new Label() { Text = label + ":", X = 1, Y = y, ColorScheme = Colors.ColorSchemes["Menu"] });

            if (browser is not null && IsLink(label))
            {
                var linkUrl = value;
                var link = new Button()
                {
                    Text = UnicodeWidth.Truncate(value, 50),
                    X = 20, Y = y,
                    ColorScheme = Theme.Link,
                };
                link.Accepting += (sender, e) => browser.OpenUrl(linkUrl);
                Add(link);
            }
            else
            {
                var valueLabel = new Label() { Text = UnicodeWidth.Truncate(value, 52), X = 20, Y = y };
                if (string.Equals(label, "Status", StringComparison.Ordinal))
                    valueLabel.ColorScheme = string.Equals(value, Glyphs.StatusActive, StringComparison.Ordinal) ? Theme.StatusActive : Theme.StatusRemoved;
                Add(valueLabel);
            }

            if (string.Equals(label, "URL", StringComparison.Ordinal)) url = value;
            y++;
        }

        if (hasDescription)
        {
            y++;
            Add(new Label() { Text = "Description:", X = 1, Y = y, ColorScheme = Colors.ColorSchemes["Menu"] });
            y++;
            var descView = new TextView()
            {
                Text = description!,
                X = 1, Y = y,
                Width = Dim.Fill(2),
                Height = Dim.Fill(2),
                ReadOnly = true,
                WordWrap = true,
                ColorScheme = Colors.ColorSchemes["Base"],
            };
            Add(descView);
        }

        if (browser is not null && url is not null)
        {
            var captured = url;
            var openBtn = new Button() { Text = "Open in Browser" };
            openBtn.Accepting += (sender, e) => browser.OpenUrl(captured);
            AddButton(openBtn);
        }

        var closeBtn = new Button() { Text = "Close", IsDefault = true };
        closeBtn.Accepting += (sender, e) => global::Terminal.Gui.Application.RequestStop();
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
            ("Active Videos", activeCount.ToString()),
            ("Removed", removedCount.ToString()),
            ("Description", playlist.Description ?? "-"),
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
            fields.Add(("Status", Glyphs.StatusRemovedFallback));
            fields.Add(("Removed At", video.DeletedAt.Value.ToString("yyyy-MM-dd HH:mm")));
            fields.Add(("Reason", video.RemovalReason?.ToString() ?? "Unknown"));
        }
        else
        {
            fields.Add(("Status", Glyphs.StatusActive));
        }

        fields.Add(("Description", video.Description ?? "-"));

        return new DetailDialog("Video Details", browser, [.. fields]);
    }
}
