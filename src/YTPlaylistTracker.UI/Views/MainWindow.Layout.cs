using System.Data;
using Terminal.Gui;

namespace YTPlaylistTracker.UI.Views;

public partial class MainWindow
{
    private FrameView _profileFrame = null!;
    private FrameView _playlistFrame = null!;
    private Label _hintBar1 = null!;
    private Label _hintBar2 = null!;

    private void SetupUI()
    {
        Title = "ytpt - YouTube Playlist Tracker";
        BorderStyle = LineStyle.Rounded;

        _profileFrame = new FrameView()
        {
            Title = "Profiles",
            X = 0, Y = 0,
            Width = 18,
            Height = Dim.Fill(2),
            ColorScheme = Theme.Frame,
            BorderStyle = LineStyle.Rounded,
        };
        _profileList = new ListView
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = Colors.ColorSchemes["Base"],
        };
        _profileList.SelectedItemChanged += OnProfileSelected;
        _profileFrame.Add(_profileList);

        _playlistFrame = new FrameView()
        {
            Title = "Playlists",
            X = Pos.Right(_profileFrame),
            Y = 0,
            Width = 28,
            Height = Dim.Fill(2),
            ColorScheme = Theme.Frame,
            BorderStyle = LineStyle.Rounded,
        };
        _playlistList = new ListView
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = Colors.ColorSchemes["Base"],
        };
        _playlistList.SelectedItemChanged += OnPlaylistSelected;
        _playlistFrame.Add(_playlistList);

        _videoFrame = new FrameView()
        {
            Title = "Videos",
            X = Pos.Right(_playlistFrame),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
            ColorScheme = Theme.Frame,
            BorderStyle = LineStyle.Rounded,
        };
        _videoTable = new TableView
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            FullRowSelect = true,
            ColorScheme = Colors.ColorSchemes["Base"],
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
        _videoTable.CellActivated += (sender, e) => ShowDetail();
        _videoFrame.Add(_videoTable);
        _videoTable.DrawComplete += (sender, e) => OnVideoTableResized();

        _playlistList.OpenSelectedItem += (sender, e) => ShowDetail();

        Add(_profileFrame, _playlistFrame, _videoFrame);

        _hintBar1 = new Label()
        {
            Text = "",
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill(),
            ColorScheme = Theme.HintKey,
        };
        _hintBar2 = new Label()
        {
            Text = " e F7 export │ H F11 hist │ F9 settings │ ? F12 help │ q quit",
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill(),
            ColorScheme = Theme.HintKey,
        };
        Add(_hintBar1, _hintBar2);
        UpdateHintBar();
    }

    private void UpdateHintBar()
    {
        if (_profileList.HasFocus)
            _hintBar1.Text = " n new │ L login │ r rename │ d default │ x delete │ Enter menu │ h/l pane │ j/k nav";
        else if (_playlistList.HasFocus)
            _hintBar1.Text = " a F1 add │ t F2 track │ T all │ s F5 sync │ S F6 all │ Enter detail │ h/l pane │ j/k nav";
        else
            _hintBar1.Text = " / F3 search │ o F4 sort │ F8 deleted │ Enter detail │ h/l pane │ j/k nav";
    }

    internal void ReapplyTheme()
    {
        _profileFrame.ColorScheme = Theme.Frame;
        _playlistFrame.ColorScheme = Theme.Frame;
        _videoFrame.ColorScheme = Theme.Frame;
        _profileList.ColorScheme = Colors.ColorSchemes["Base"];
        _playlistList.ColorScheme = Colors.ColorSchemes["Base"];
        _videoTable.ColorScheme = Colors.ColorSchemes["Base"];
        _hintBar1.ColorScheme = Theme.HintKey;
        _hintBar2.ColorScheme = Theme.HintKey;
        ApplyDefaultColorScheme();
        SetNeedsDraw();
    }

    private void ShowSpinner(string message)
    {
        HideSpinner();
        _spinnerFrame = 0;
        ColorScheme = Theme.Syncing;
        _spinnerTimer = global::Terminal.Gui.Application.AddTimeout(TimeSpan.FromMilliseconds(200), () =>
        {
            Title = "ytpt " + SpinnerFrames[_spinnerFrame % SpinnerFrames.Length] + " " + message;
            _spinnerFrame++;
            return true;
        });
    }

    private void HideSpinner()
    {
        if (_spinnerTimer is not null)
        {
            global::Terminal.Gui.Application.RemoveTimeout(_spinnerTimer);
            _spinnerTimer = null;
        }
        Title = DefaultTitle;
        ApplyDefaultColorScheme();
        SetNeedsDraw();
    }

    private void ApplyDefaultColorScheme()
    {
        if (_updateInstalled)
            ColorScheme = Theme.UpdateInstalled;
        else if (_latestUpdate is { IsUpdateAvailable: true })
            ColorScheme = Theme.UpdateAvailable;
        else
            ColorScheme = Colors.ColorSchemes["Base"];
    }
}
