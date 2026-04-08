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
        _profileFrame = new FrameView("Profiles")
        {
            X = 0, Y = 0,
            Width = 18,
            Height = Dim.Fill(2),
            ColorScheme = Theme.Frame,
        };
        _profileList = new ListView
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = Colors.Base,
        };
        _profileList.SelectedItemChanged += OnProfileSelected;
        _profileFrame.Add(_profileList);

        _playlistFrame = new FrameView("Playlists")
        {
            X = Pos.Right(_profileFrame),
            Y = 0,
            Width = 28,
            Height = Dim.Fill(2),
            ColorScheme = Theme.Frame,
        };
        _playlistList = new ListView
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ColorScheme = Colors.Base,
        };
        _playlistList.SelectedItemChanged += OnPlaylistSelected;
        _playlistFrame.Add(_playlistList);

        _videoFrame = new FrameView("Videos")
        {
            X = Pos.Right(_playlistFrame),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
            ColorScheme = Theme.Frame,
        };
        _videoTable = new TableView
        {
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            FullRowSelect = true,
            ColorScheme = Colors.Base,
            Style = new TableView.TableStyle
            {
                ShowVerticalCellLines = false,
                ShowVerticalHeaderLines = false,
                ShowHorizontalHeaderOverline = false,
                ShowHorizontalHeaderUnderline = true,
                ExpandLastColumn = false,
                AlwaysShowHeaders = true,
                ColumnStyles = new Dictionary<DataColumn, TableView.ColumnStyle>(),
            },
        };
        _videoTable.CellActivated += (args) => ShowDetail();
        _videoFrame.Add(_videoTable);
        _videoTable.LayoutComplete += (_) => OnVideoTableResized();

        _playlistList.OpenSelectedItem += (args) => ShowDetail();

        Add(_profileFrame, _playlistFrame, _videoFrame);

        _hintBar1 = new Label("")
        {
            Y = Pos.AnchorEnd(2),
            Width = Dim.Fill(),
            ColorScheme = Theme.HintKey,
        };
        _hintBar2 = new Label(" e F7 export │ H F11 hist │ F9 settings │ ? F12 help │ q quit")
        {
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
        _profileList.ColorScheme = Colors.Base;
        _playlistList.ColorScheme = Colors.Base;
        _videoTable.ColorScheme = Colors.Base;
        _hintBar1.ColorScheme = Theme.HintKey;
        _hintBar2.ColorScheme = Theme.HintKey;
        ApplyDefaultColorScheme();
        SetNeedsDisplay();
    }

    private void ShowSpinner(string message)
    {
        HideSpinner();
        _spinnerFrame = 0;
        ColorScheme = Theme.Syncing;
        _spinnerTimer = global::Terminal.Gui.Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(200), _ =>
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
            global::Terminal.Gui.Application.MainLoop.RemoveTimeout(_spinnerTimer);
            _spinnerTimer = null;
        }
        Title = DefaultTitle;
        ApplyDefaultColorScheme();
        SetNeedsDisplay();
    }

    private void ApplyDefaultColorScheme()
    {
        if (_updateInstalled)
            ColorScheme = Theme.UpdateInstalled;
        else if (_latestUpdate is { IsUpdateAvailable: true })
            ColorScheme = Theme.UpdateAvailable;
        else
            ColorScheme = Colors.Base;
    }
}
