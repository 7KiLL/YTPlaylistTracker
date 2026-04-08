using Terminal.Gui;

namespace YTPlaylistTracker.UI.Views;

public partial class MainWindow
{
    protected override bool OnKeyDown(Key key)
    {
        // === ProcessHotKey logic (runs first) ===

        // Don't intercept vim/letter keys when search field is active
        bool searchFocused = _searchField is not null && _searchField.HasFocus;

        if (!searchFocused)
        {
            // Pane switching (arrows + h/l)
            View[] panes = [_profileList, _playlistList, _videoTable];
            var current = Array.FindIndex(panes, p => p.HasFocus);

            if ((key == Key.CursorLeft || key == (Key)'h') && current > 0)
            {
                panes[current - 1].SetFocus();
                UpdateHintBar();
                return true;
            }

            if ((key == Key.CursorRight || key == (Key)'l') && current >= 0 && current < panes.Length - 1)
            {
                panes[current + 1].SetFocus();
                UpdateHintBar();
                return true;
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
                return true;
            }

            if (key == Key.CursorUp.WithShift)
            {
                for (int i = 0; i < 5; i++)
                    focused.NewKeyDownEvent(Key.CursorUp);
                return true;
            }

            // Profile-specific hotkeys when profile pane has focus
            if (_profileList.HasFocus)
            {
                if (key == (Key)'n') { OnNewProfile(); return true; }
                if (key == (Key)'L') { OnToggleLogin(); return true; }
                if (key == (Key)'r') { OnRenameProfile(); return true; }
                if (key == (Key)'d') { OnSetDefaultProfile(); return true; }
                if (key == (Key)'x') { OnDeleteProfile(); return true; }

                if (key == Key.Enter)
                {
                    ShowProfileContextMenu();
                    return true;
                }
            }

            // Shift+J/K (uppercase) = fast scroll; lowercase = single step
            if (key == (Key)'J')
            {
                for (int i = 0; i < 5; i++)
                    focused.NewKeyDownEvent(Key.CursorDown);
                return true;
            }

            if (key == (Key)'K')
            {
                for (int i = 0; i < 5; i++)
                    focused.NewKeyDownEvent(Key.CursorUp);
                return true;
            }

            if (key == (Key)'j') { focused.NewKeyDownEvent(Key.CursorDown); return true; }
            if (key == (Key)'k') { focused.NewKeyDownEvent(Key.CursorUp); return true; }
            if (key == (Key)'a') { _ = OnAddByUrlAsync(); return true; }
            if (key == (Key)'t') { OnToggleTrack(); return true; }
            if (key == (Key)'T') { OnToggleAllTracking(); return true; }
            if (key == (Key)'s') { OnSync(); return true; }
            if (key == (Key)'S') { OnSyncAll(); return true; }
            if (key == (Key)'e') { _ = OnExport(); return true; }
            if (key == (Key)'H') { _ = OnShowHistory(); return true; }
            if (key == (Key)'o') { ShowSortMenu(); return true; }
            if (key == (Key)'u') { OnUpdateCheck(); return true; }
            if (key == (Key)'q') { global::Terminal.Gui.Application.RequestStop(); return true; }
            if (key == (Key)'/') { ShowSearch(); return true; }

            if (key == (Key)'?')
            {
                global::Terminal.Gui.Application.Run(new HelpDialog());
                return true;
            }
        }

        // === ProcessKey logic (Tab, F-keys, Ctrl combos — always active) ===

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
