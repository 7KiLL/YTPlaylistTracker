using Terminal.Gui;

namespace YTPlaylistTracker.UI.Views;

public partial class MainWindow
{
    public override bool ProcessHotKey(KeyEvent keyEvent)
    {
        // Don't intercept keys when search field is active
        if (_searchField is not null && _searchField.HasFocus)
            return base.ProcessHotKey(keyEvent);

        // Pane switching (arrows + h/l)
        View[] panes = [_profileList, _playlistList, _videoTable];
        var current = Array.FindIndex(panes, p => p.HasFocus);

        switch (keyEvent.Key)
        {
            case Key.CursorLeft or Key.h when current > 0:
                panes[current - 1].SetFocus();
                return true;
            case Key.CursorRight or Key.l when current >= 0 && current < panes.Length - 1:
                panes[current + 1].SetFocus();
                return true;
        }

        // Focused pane for j/k navigation
        var focused = (_profileList.HasFocus, _playlistList.HasFocus) switch
        {
            (true, _) => (View)_profileList,
            (_, true) => (View)_playlistList,
            _ => (View)_videoTable
        };

        // Shift+Arrow / Shift+j/k: fast scroll (5 rows at a time)
        switch (keyEvent.Key)
        {
            case Key.CursorDown | Key.ShiftMask:
            case Key.J | Key.ShiftMask:
                for (int i = 0; i < 5; i++)
                    focused.ProcessKey(new KeyEvent(Key.CursorDown, new KeyModifiers()));
                return true;
            case Key.CursorUp | Key.ShiftMask:
            case Key.K | Key.ShiftMask:
                for (int i = 0; i < 5; i++)
                    focused.ProcessKey(new KeyEvent(Key.CursorUp, new KeyModifiers()));
                return true;
        }

        // All single-letter keybinds in ProcessHotKey so child views don't eat them
        switch (keyEvent.KeyValue)
        {
            case 'j': focused.ProcessKey(new KeyEvent(Key.CursorDown, new KeyModifiers())); return true;
            case 'k': focused.ProcessKey(new KeyEvent(Key.CursorUp, new KeyModifiers())); return true;
            case 'a': _ = OnAddByUrlAsync(); return true;
            case 't': OnToggleTrack(); return true;
            case 'T': OnToggleAllTracking(); return true;
            case 's': OnSync(); return true;
            case 'S': OnSyncAll(); return true;
            case 'e': _ = OnExport(); return true;
            case 'H': _ = OnShowHistory(); return true;
            case 'o': ShowSortMenu(); return true;
            case 'u': OnUpdateCheck(); return true;
            case 'q': global::Terminal.Gui.Application.RequestStop(); return true;
            case '/': ShowSearch(); return true;
            case '?':
                global::Terminal.Gui.Application.Run(new HelpDialog());
                return true;
        }

        return base.ProcessHotKey(keyEvent);
    }

    public override bool ProcessKey(KeyEvent keyEvent)
    {
        // Tab / Shift+Tab: cycle focus between the three panes
        if (keyEvent.Key == Key.Tab || keyEvent.Key == Key.BackTab)
        {
            View[] panes = [_profileList, _playlistList, _videoTable];
            var current = Array.FindIndex(panes, p => p.HasFocus);
            if (current < 0) current = 0;
            int next = keyEvent.Key == Key.Tab
                ? (current + 1) % panes.Length
                : (current - 1 + panes.Length) % panes.Length;
            panes[next].SetFocus();
            return true;
        }

        // F-keys in ProcessKey (child views don't consume them)
        switch (keyEvent.Key)
        {
            case Key.F1: _ = OnAddByUrlAsync(); return true;
            case Key.F2: OnToggleTrack(); return true;
            case Key.F5: OnSync(); return true;
            case Key.F6: OnSyncAll(); return true;
            case Key.F8: OnToggleDeleted(); return true;
            case Key.F9: OnSettings(); return true;
            case Key.F10: global::Terminal.Gui.Application.RequestStop(); return true;
        }

        if (keyEvent.Key == (Key.C | Key.CtrlMask))
        {
            var now = DateTime.UtcNow;
            if ((now - _lastCtrlC).TotalMilliseconds < 1000)
            {
                global::Terminal.Gui.Application.RequestStop();
                return true;
            }
            _lastCtrlC = now;
            Title = "ytpt - Press Ctrl+C again to quit";
            SetNeedsDisplay();
            global::Terminal.Gui.Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(2), _ =>
            {
                Title = DefaultTitle;
                SetNeedsDisplay();
                return false;
            });
            return true;
        }

        if (keyEvent.Key == (Key.U | Key.CtrlMask))
        {
            OnUpdateCheck();
            return true;
        }

        return base.ProcessKey(keyEvent);
    }
}
