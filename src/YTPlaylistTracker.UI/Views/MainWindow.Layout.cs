using System.Data;

namespace YTPlaylistTracker.UI.Views;

public partial class MainWindow
{
    internal FrameView _profileFrame = null!;
    private FrameView _playlistFrame = null!;
    private Label _profileHeaderLabel = null!;
    private Label _playlistHeaderLabel = null!;
    private Label _videoStatusLabel = null!;
    internal Label _hintBar1 = null!;
    internal Label _hintBar2 = null!;
    internal bool _profilePaneVisible = true;

    private void SetupUI()
    {
        Title = " ytpt - YouTube Playlist Tracker ";
        BorderStyle = LineStyle.Rounded;
        Border!.Thickness = new Thickness(1, 2, 1, 1);

        _profileFrame = new FrameView()
        {
            X = 0, Y = 0,
            Width = Dim.Func(_ => Math.Max(18, Viewport.Width * 15 / 100)),
            Height = Dim.Fill(2),
            SchemeName = Theme.SchemeFrame,
            BorderStyle = LineStyle.Rounded,
        };
        _profileHeaderLabel = new Label()
        {
            Text = " Profiles",
            X = 0, Y = 0,
            Width = Dim.Fill(),
            SchemeName = Theme.SchemeFrame,
        };
        _profileList = new ListView
        {
            X = 1, Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(),
            SchemeName = "Base",
        };
        _profileList.ValueChanged += OnProfileSelected;
        _profileFrame.Add(_profileHeaderLabel, _profileList);

        _playlistFrame = new FrameView()
        {
            X = Pos.Right(_profileFrame),
            Y = 0,
            Width = Dim.Func(_ => Math.Max(28, Viewport.Width * 25 / 100)),
            Height = Dim.Fill(2),
            SchemeName = Theme.SchemeFrame,
            BorderStyle = LineStyle.Rounded,
        };
        _playlistHeaderLabel = new Label()
        {
            Text = " Playlists",
            X = 0, Y = 0,
            Width = Dim.Fill(),
            SchemeName = Theme.SchemeFrame,
        };
        _playlistList = new ListView
        {
            X = 1, Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(),
            SchemeName = "Base",
        };
        _playlistList.ValueChanged += OnPlaylistSelected;
        _playlistFrame.Add(_playlistHeaderLabel, _playlistList);

        _videoFrame = new FrameView()
        {
            X = Pos.Right(_playlistFrame),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
            SchemeName = Theme.SchemeFrame,
            BorderStyle = LineStyle.Rounded,
        };
        _videoStatusLabel = new Label()
        {
            Text = " Videos",
            X = 0, Y = 0,
            Width = Dim.Fill(),
            SchemeName = Theme.SchemeFrame,
        };
        _videoTable = new TableView
        {
            X = 1, Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(),
            FullRowSelect = true,
            SchemeName = "Base",
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
        // Remove arrow key bindings so they propagate to MainWindow for pane switching
        _videoTable.KeyBindings.Remove(Key.CursorLeft);
        _videoTable.KeyBindings.Remove(Key.CursorRight);
        _videoTable.CellActivated += (sender, e) => ShowDetail();
        _videoFrame.Add(_videoStatusLabel, _videoTable);
        _videoTable.DrawComplete += (sender, e) => OnVideoTableResized();

        _playlistList.Accepting += (sender, e) => ShowDetail();

        Add(_profileFrame, _playlistFrame, _videoFrame);

        _hintBar1 = new Label()
        {
            Text = "",
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill(),
            SchemeName = Theme.SchemeHintKey,
        };
        _hintBar2 = new Label()
        {
            Text = " e F7 export │ H F11 hist │ F9 settings │ p profile │ ? F12 help │ q quit",
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            SchemeName = Theme.SchemeHintKey,
        };
        _spinner = new SpinnerView
        {
            Style = new SpinnerStyle.Dots(),
            AutoSpin = false,
            X = 1,
            Y = Pos.AnchorEnd(3),
            Visible = false,
        };
        _spinnerMessage = new Label
        {
            Text = "",
            X = Pos.Right(_spinner) + 1,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill(),
            Visible = false,
            SchemeName = Theme.SchemeHintKey,
        };
        Add(_spinner, _spinnerMessage, _hintBar1, _hintBar2);
        UpdateHintBar();
    }

    private void UpdateHintBar()
    {
        var profileHint = !_profilePaneVisible ? " p profiles │" : "";
        if (_profilePaneVisible && _profileList.HasFocus)
            _hintBar1.Text = " n new │ L login │ r rename │ d default │ x delete │ Enter menu │ h/l pane │ j/k nav";
        else if (_playlistList.HasFocus)
            _hintBar1.Text = profileHint + " a F1 add │ t F2 track │ T all │ s F5 sync │ S F6 all │ Enter detail │ h/l pane │ j/k nav";
        else
            _hintBar1.Text = profileHint + " / F3 search │ o F4 sort │ F8 deleted │ Enter detail │ h/l pane │ j/k nav";
    }

    private void ToggleProfilePane()
    {
        _profilePaneVisible = !_profilePaneVisible;
        _profileFrame.Visible = _profilePaneVisible;
        _playlistFrame.X = _profilePaneVisible ? Pos.Right(_profileFrame) : 0;
        if (!_profilePaneVisible && _profileList.HasFocus)
            _playlistList.SetFocus();
        UpdateHintBar();
        SetNeedsDraw();
    }

    internal void ReapplyTheme()
    {
        _profileFrame.SchemeName = Theme.SchemeFrame;
        _playlistFrame.SchemeName = Theme.SchemeFrame;
        _videoFrame.SchemeName = Theme.SchemeFrame;
        _profileHeaderLabel.SchemeName = Theme.SchemeFrame;
        _playlistHeaderLabel.SchemeName = Theme.SchemeFrame;
        _videoStatusLabel.SchemeName = Theme.SchemeFrame;
        _profileList.SchemeName = "Base";
        _playlistList.SchemeName = "Base";
        _videoTable.SchemeName = "Base";
        _hintBar1.SchemeName = Theme.SchemeHintKey;
        _hintBar2.SchemeName = Theme.SchemeHintKey;
        _spinnerMessage.SchemeName = Theme.SchemeHintKey;
        ApplyDefaultScheme();
        SetNeedsDraw();
    }

    private void ShowSpinner(string message)
    {
        _spinnerMessage.Text = " " + message;
        _spinner.Visible = true;
        _spinnerMessage.Visible = true;
        _spinner.AutoSpin = true;
        SchemeName = Theme.SchemeSyncing;
        Title = " ytpt - syncing ";
    }

    private void HideSpinner()
    {
        _spinner.AutoSpin = false;
        _spinner.Visible = false;
        _spinnerMessage.Visible = false;
        Title = DefaultTitle;
        ApplyDefaultScheme();
        SetNeedsDraw();
    }

    private void ApplyDefaultScheme()
    {
        if (_updateInstalled)
            SchemeName = Theme.SchemeUpdateInstalled;
        else if (_latestUpdate is { IsUpdateAvailable: true })
            SchemeName = Theme.SchemeUpdateAvailable;
        else
            SchemeName = "Base";
    }
}
