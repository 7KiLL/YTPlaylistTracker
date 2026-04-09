using Terminal.Gui;
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
            ColorScheme = Theme.Frame,
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
            lbl.MouseClick += (sender, e) => SelectTab(idx);
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

        KeyDown += (sender, e) =>
        {
            if (e.KeyCode == KeyCode.CursorLeft && _selectedTab > 0)
                { SelectTab(_selectedTab - 1); e.Handled = true; }
            else if (e.KeyCode == KeyCode.CursorRight && _selectedTab < _tabLabels.Count - 1)
                { SelectTab(_selectedTab + 1); e.Handled = true; }
        };

        var closeBtn = new Button() { Text = "Close", IsDefault = true };
        closeBtn.Accepting += (sender, e) => global::Terminal.Gui.Application.RequestStop();
        AddButton(closeBtn);
    }

    private void SelectTab(int index)
    {
        _selectedTab = index;
        for (int i = 0; i < _tabLabels.Count; i++)
        {
            _tabLabels[i].ColorScheme = i == index ? Theme.SectionHeader : Theme.Frame;
            _tabPages[i].Visible = i == index;
        }
        _tabPages[index].SetFocus();
        SetNeedsDraw();
    }

    private static void ReapplyAllSchemes(View root)
    {
        root.ColorScheme = Colors.ColorSchemes["Dialog"];
        foreach (var view in root.Subviews)
        {
            if (view is Label lbl && lbl.Text?.ToString()?.StartsWith("──", StringComparison.Ordinal) == true)
                lbl.ColorScheme = Theme.SectionHeader;
            else if (view is Button btn && (btn.Text?.ToString()?.Contains("Purge", StringComparison.Ordinal) == true
                || btn.Text?.ToString()?.Contains("Reset", StringComparison.Ordinal) == true))
                btn.ColorScheme = Theme.Danger;
            else
                view.ColorScheme = Colors.ColorSchemes["Dialog"];

            ReapplyAllSchemes(view);
        }
    }
}
