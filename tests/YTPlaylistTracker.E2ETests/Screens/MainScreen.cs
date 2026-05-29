using YTPlaylistTracker.UI.Views;

namespace YTPlaylistTracker.E2ETests.Screens;

internal sealed class MainScreen
{
    private readonly MainWindow _window;

    public MainScreen(MainWindow window) => _window = window;

    public enum Pane { Profile, Playlist, Video }

    public Pane FocusedPane =>
        _window._profileList.HasFocus ? Pane.Profile :
        _window._playlistList.HasFocus ? Pane.Playlist :
        Pane.Video;

    public bool IsProfilePaneVisible => _window._profilePaneVisible;
    public int ProfileCount => _window._profileList.Source?.Count ?? 0;
    public int? SelectedProfileIndex => _window._profileList.SelectedItem;
    public int PlaylistCount => _window._playlistList.Source?.Count ?? 0;
    public int? SelectedPlaylistIndex => _window._playlistList.SelectedItem;
    public int VideoRowCount => _window._videoTable.Table?.Rows ?? 0;
    public int SelectedVideoRow => _window._videoTable.Value?.SelectedCell.Y ?? -1;
    public string Title => _window.Title ?? "";
    public bool IsSpinnerVisible => _window._spinner.Visible;
    public string SpinnerMessage => _window._spinnerMessage.Text ?? "";
    public bool ShowDeletedOnly => _window._showDeletedOnly;
    public int FilteredVideoCount => _window._filteredVideos.Count;
}
