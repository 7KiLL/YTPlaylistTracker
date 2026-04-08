using Terminal.Gui;

namespace YTPlaylistTracker.UI.Views;

public partial class MainWindow
{
    private void SetupKeyHandling()
    {
        // global::Terminal.Gui.Application.KeyDown fires BEFORE any view processes the key.
        // This is the v2 equivalent of v1's ProcessHotKey (parent-first).
        // We use it for vim-style letter keys that child views would otherwise consume.
        global::Terminal.Gui.Application.KeyDown += OnApplicationKeyDown;
    }

    private void CleanupKeyHandling()
    {
        global::Terminal.Gui.Application.KeyDown -= OnApplicationKeyDown;
    }

    private void OnApplicationKeyDown(object? sender, Key key)
    {
        // Only intercept when MainWindow is the active toplevel (not during modal dialogs)
        if (global::Terminal.Gui.Application.Top != this)
            return;

        // Don't intercept when any text input has focus
        if (global::Terminal.Gui.Application.Top?.MostFocused is TextField or TextView)
            return;

        // Don't intercept when search field is active
        if (_searchField is not null && _searchField.HasFocus)
            return;

        // Pane switching (arrows + h/l)
        View[] panes = [_profileList, _playlistList, _videoTable];
        var current = Array.FindIndex(panes, p => p.HasFocus);

        if ((key == Key.CursorLeft || key == (Key)'h') && current > 0)
        {
            panes[current - 1].SetFocus();
            UpdateHintBar();
            key.Handled = true;
            return;
        }

        if ((key == Key.CursorRight || key == (Key)'l') && current >= 0 && current < panes.Length - 1)
        {
            panes[current + 1].SetFocus();
            UpdateHintBar();
            key.Handled = true;
            return;
        }

        // Focused pane for j/k navigation
        var focused = (_profileList.HasFocus, _playlistList.HasFocus) switch
        {
            (true, _) => (View)_profileList,
            (_, true) => (View)_playlistList,
            _ => (View)_videoTable,
        };

        // Shift+Arrow: fast scroll (5 rows at a time)
        if (key == Key.CursorDown.WithShift)
        {
            for (int i = 0; i < 5; i++)
                focused.NewKeyDownEvent(Key.CursorDown);
            key.Handled = true;
            return;
        }

        if (key == Key.CursorUp.WithShift)
        {
            for (int i = 0; i < 5; i++)
                focused.NewKeyDownEvent(Key.CursorUp);
            key.Handled = true;
            return;
        }

        // Profile-specific hotkeys when profile pane has focus
        if (_profileList.HasFocus)
        {
            if (key == (Key)'n') { OnNewProfile(); key.Handled = true; return; }
            if (key == (Key)'L') { OnToggleLogin(); key.Handled = true; return; }
            if (key == (Key)'r') { OnRenameProfile(); key.Handled = true; return; }
            if (key == (Key)'d') { OnSetDefaultProfile(); key.Handled = true; return; }
            if (key == (Key)'x') { OnDeleteProfile(); key.Handled = true; return; }

            if (key == Key.Enter)
            {
                ShowProfileContextMenu();
                key.Handled = true;
                return;
            }
        }

        // Shift+J/K (uppercase) = fast scroll; lowercase = single step
        if (key == (Key)'J')
        {
            for (int i = 0; i < 5; i++)
                focused.NewKeyDownEvent(Key.CursorDown);
            key.Handled = true;
            return;
        }

        if (key == (Key)'K')
        {
            for (int i = 0; i < 5; i++)
                focused.NewKeyDownEvent(Key.CursorUp);
            key.Handled = true;
            return;
        }

        if (key == (Key)'j') { focused.NewKeyDownEvent(Key.CursorDown); key.Handled = true; return; }
        if (key == (Key)'k') { focused.NewKeyDownEvent(Key.CursorUp); key.Handled = true; return; }
        if (key == (Key)'a') { _ = OnAddByUrlAsync(); key.Handled = true; return; }
        if (key == (Key)'t') { OnToggleTrack(); key.Handled = true; return; }
        if (key == (Key)'T') { OnToggleAllTracking(); key.Handled = true; return; }
        if (key == (Key)'s') { OnSync(); key.Handled = true; return; }
        if (key == (Key)'S') { OnSyncAll(); key.Handled = true; return; }
        if (key == (Key)'e') { _ = OnExport(); key.Handled = true; return; }
        if (key == (Key)'H') { _ = OnShowHistory(); key.Handled = true; return; }
        if (key == (Key)'o') { ShowSortMenu(); key.Handled = true; return; }
        if (key == (Key)'u') { OnUpdateCheck(); key.Handled = true; return; }
        if (key == (Key)'q') { global::Terminal.Gui.Application.RequestStop(); key.Handled = true; return; }
        if (key == (Key)'/') { ShowSearch(); key.Handled = true; return; }

        if (key == (Key)'?')
        {
            global::Terminal.Gui.Application.Run(new HelpDialog());
            key.Handled = true;
            return;
        }
    }

    protected override bool OnKeyDown(Key key)
    {
        // Tab / Shift+Tab: cycle focus between the three panes
        if (key == Key.Tab || key == Key.Tab.WithShift)
        {
            View[] panes = [_profileList, _playlistList, _videoTable];
            var current = Array.FindIndex(panes, p => p.HasFocus);
            if (current < 0) current = 0;
            int next = key == Key.Tab
                ? (current + 1) % panes.Length
                : (current - 1 + panes.Length) % panes.Length;
            panes[next].SetFocus();
            UpdateHintBar();
            return true;
        }

        // F-keys
        if (key == Key.F1) { _ = OnAddByUrlAsync(); return true; }
        if (key == Key.F2) { OnToggleTrack(); return true; }
        if (key == Key.F3) { ShowSearch(); return true; }
        if (key == Key.F4) { ShowSortMenu(); return true; }
        if (key == Key.F5) { OnSync(); return true; }
        if (key == Key.F6) { OnSyncAll(); return true; }
        if (key == Key.F7) { _ = OnExport(); return true; }
        if (key == Key.F8) { OnToggleDeleted(); return true; }
        if (key == Key.F9) { OnSettings(); return true; }
        if (key == Key.F10) { global::Terminal.Gui.Application.RequestStop(); return true; }
        if (key == Key.F11) { _ = OnShowHistory(); return true; }

        if (key == Key.F12)
        {
            global::Terminal.Gui.Application.Run(new HelpDialog());
            return true;
        }

        if (key == Key.C.WithCtrl)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastCtrlC).TotalMilliseconds < 1000)
            {
                global::Terminal.Gui.Application.RequestStop();
                return true;
            }
            _lastCtrlC = now;
            Title = "ytpt - Press Ctrl+C again to quit";
            SetNeedsDraw();
            global::Terminal.Gui.Application.AddTimeout(TimeSpan.FromSeconds(2), () =>
            {
                Title = DefaultTitle;
                SetNeedsDraw();
                return false;
            });
            return true;
        }

        if (key == Key.U.WithCtrl)
        {
            OnUpdateCheck();
            return true;
        }

        return base.OnKeyDown(key);
    }
}
