using YTPlaylistTracker.Domain.Entities;
using YTPlaylistTracker.Domain.Interfaces;
using YTPlaylistTracker.Domain.Models;
using YTPlaylistTracker.Infrastructure.Platform;
using YTPlaylistTracker.Infrastructure.Update;

namespace YTPlaylistTracker.UI.Views;

public sealed partial class SettingsDialog : Dialog
{
    public bool UpdateRequested { get; private set; }
    public UpdateInfo? UpdateInfo { get; private set; }

    private readonly List<Label> _tabLabels = [];
    private readonly List<View> _tabPages = [];
    private int _selectedTab;
    private IApplication? _app;

    public SettingsDialog(IPlaylistRepository playlistRepo, Playlist? selectedPlaylist,
        IUserSettings userSettings, IUpdateService updateService, ISystemLauncher? launcher = null,
        MainWindow? mainWindow = null)
        : base()
    {
        Title = "";
        Width = 70;
        Height = 30;
        BorderStyle = LineStyle.Rounded;
        Border!.Settings &= ~BorderSettings.Title;

        Add(new Label()
        {
            Text = " Settings",
            X = 0, Y = 0,
            Width = Dim.Fill(),
            SchemeName = Theme.SchemeFrame,
        });

        // Tab labels row
        var tabNames = new[] { "General", "Display", "Storage", "Sync" };
        int tabX = 1;
        for (int i = 0; i < tabNames.Length; i++)
        {
            var idx = i;
            var text = $" {tabNames[i]} ";
            var lbl = new Label()
            {
                Text = text,
                X = tabX, Y = 2,
                CanFocus = true,
            };
            lbl.MouseEvent += (sender, e) => { if (e.Flags.HasFlag(MouseFlags.LeftButtonClicked)) SelectTab(idx); };
            lbl.KeyDown += (sender, e) =>
            {
                if (e.KeyCode == KeyCode.Enter || e.KeyCode == KeyCode.Space)
                    SelectTab(idx);
                else if (e.KeyCode == KeyCode.CursorRight && idx < tabNames.Length - 1)
                    { SelectTab(idx + 1); e.Handled = true; }
                else if (e.KeyCode == KeyCode.CursorLeft && idx > 0)
                    { SelectTab(idx - 1); e.Handled = true; }
            };
            _tabLabels.Add(lbl);
            Add(lbl);
            tabX += text.Length + 1;
        }

        // Tab content pages
        _tabPages.Add(BuildGeneralTab(userSettings, updateService, mainWindow));
        _tabPages.Add(BuildDisplayTab(userSettings));
        _tabPages.Add(BuildStorageTab(playlistRepo, selectedPlaylist, launcher));
        _tabPages.Add(BuildSyncTab(userSettings));

        foreach (var page in _tabPages)
        {
            page.X = 0;
            page.Y = 4;
            page.Width = Dim.Fill();
            page.Height = Dim.Fill(1);
            page.Visible = false;
            Add(page);
        }

        SelectTab(0);

        var closeBtn = new Button() { Text = "Close", IsDefault = true };
        closeBtn.Accepting += (sender, e) => App!.RequestStop();
        AddButton(closeBtn);

        // App-level KeyDown fires before any view — needed because OptionSelector
        // consumes Left/Right before OnKeyDown fires. Wire once App is available
        // (Initialized); capture the app so we can reliably unsubscribe on dispose.
        Initialized += (_, _) => { _app = App; if (_app is not null) _app.Keyboard.KeyDown += OnSettingsKeyDown; };
        Disposing += (_, _) => { if (_app is not null) _app.Keyboard.KeyDown -= OnSettingsKeyDown; };
    }

    private void OnSettingsKeyDown(object? sender, Key key)
    {
        if (!IsCurrentTop) return;
#pragma warning disable CS0618 // TextView deprecated in 2.4.3; still detected for focus-skip
        if (_app?.Navigation?.GetFocused() is TextField or TextView) return;
#pragma warning restore CS0618

        if (key.KeyCode == KeyCode.CursorLeft && _selectedTab > 0)
            { SelectTab(_selectedTab - 1); key.Handled = true; }
        else if (key.KeyCode == KeyCode.CursorRight && _selectedTab < _tabLabels.Count - 1)
            { SelectTab(_selectedTab + 1); key.Handled = true; }
    }

    private void SelectTab(int index)
    {
        _selectedTab = index;
        for (int i = 0; i < _tabLabels.Count; i++)
        {
            _tabLabels[i].SchemeName = i == index ? Theme.SchemeSectionHeader : Theme.SchemeFrame;
            _tabPages[i].Visible = i == index;
        }
        _tabPages[index].SetFocus();
        SetNeedsDraw();
    }

    private static void ReapplyAllSchemes(View root)
    {
        root.SchemeName = "Dialog";
        foreach (var view in root.SubViews)
        {
            if (view is Label lbl && lbl.Text?.ToString()?.StartsWith("──", StringComparison.Ordinal) == true)
                lbl.SchemeName = Theme.SchemeSectionHeader;
            else if (view is Button btn && (btn.Text?.ToString()?.Contains("Purge", StringComparison.Ordinal) == true
                || btn.Text?.ToString()?.Contains("Reset", StringComparison.Ordinal) == true))
                btn.SchemeName = Theme.SchemeDanger;
            else
                view.SchemeName = "Dialog";

            ReapplyAllSchemes(view);
        }
    }
}
