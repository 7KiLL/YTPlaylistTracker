using Terminal.Gui;

namespace YTPlaylistTracker.UI.Views;

public partial class MainWindow
{
    private static readonly Dictionary<Key, AppCommand> s_globalKeys = new()
    {
        // Vim pane switching
        [(Key)'h'] = AppCommand.PaneLeft,
        [(Key)'l'] = AppCommand.PaneRight,

        // Arrow pane switching
        [Key.CursorLeft] = AppCommand.PaneLeft,
        [Key.CursorRight] = AppCommand.PaneRight,

        // Tab pane cycling
        [Key.Tab] = AppCommand.PaneNext,
        [Key.Tab.WithShift] = AppCommand.PanePrev,

        // Vim list navigation
        [(Key)'j'] = AppCommand.NavDown,
        [(Key)'k'] = AppCommand.NavUp,
        [(Key)'J'] = AppCommand.FastDown,
        [(Key)'K'] = AppCommand.FastUp,
        [Key.CursorDown.WithShift] = AppCommand.FastDown,
        [Key.CursorUp.WithShift] = AppCommand.FastUp,

        // Actions
        [(Key)'a'] = AppCommand.AddPlaylist,
        [(Key)'t'] = AppCommand.ToggleTrack,
        [(Key)'T'] = AppCommand.ToggleAllTracking,
        [(Key)'s'] = AppCommand.Sync,
        [(Key)'S'] = AppCommand.SyncAll,
        [(Key)'e'] = AppCommand.Export,
        [(Key)'H'] = AppCommand.ShowHistory,
        [(Key)'o'] = AppCommand.SortMenu,
        [(Key)'u'] = AppCommand.UpdateCheck,
        [(Key)'q'] = AppCommand.Quit,
        [(Key)'/'] = AppCommand.Search,
        [(Key)'?'] = AppCommand.Help,

        // F-key aliases
        [Key.F1] = AppCommand.AddPlaylist,
        [Key.F2] = AppCommand.ToggleTrack,
        [Key.F3] = AppCommand.Search,
        [Key.F4] = AppCommand.SortMenu,
        [Key.F5] = AppCommand.Sync,
        [Key.F6] = AppCommand.SyncAll,
        [Key.F7] = AppCommand.Export,
        [Key.F8] = AppCommand.ToggleDeleted,
        [Key.F9] = AppCommand.Settings,
        [Key.F10] = AppCommand.Quit,
        [Key.F11] = AppCommand.ShowHistory,
        [Key.F12] = AppCommand.Help,

        // Ctrl combos
        [Key.U.WithCtrl] = AppCommand.UpdateCheck,
    };

    private static readonly Dictionary<Key, AppCommand> s_profileKeys = new()
    {
        [(Key)'n'] = AppCommand.NewProfile,
        [(Key)'L'] = AppCommand.ToggleLogin,
        [(Key)'r'] = AppCommand.RenameProfile,
        [(Key)'d'] = AppCommand.SetDefault,
        [(Key)'x'] = AppCommand.DeleteProfile,
        [Key.Enter] = AppCommand.ProfileMenu,
    };

    private Dictionary<AppCommand, Func<bool>> _commands = null!;

    private void RegisterCommands()
    {
        _commands = new()
        {
            // Pane navigation
            [AppCommand.PaneLeft] = () => SwitchPane(-1),
            [AppCommand.PaneRight] = () => SwitchPane(1),
            [AppCommand.PaneNext] = () => CyclePane(1),
            [AppCommand.PanePrev] = () => CyclePane(-1),

            // List navigation
            [AppCommand.NavDown] = () => { GetFocusedPane().NewKeyDownEvent(Key.CursorDown); return true; },
            [AppCommand.NavUp] = () => { GetFocusedPane().NewKeyDownEvent(Key.CursorUp); return true; },
            [AppCommand.FastDown] = () => { var p = GetFocusedPane(); for (int i = 0; i < 5; i++) p.NewKeyDownEvent(Key.CursorDown); return true; },
            [AppCommand.FastUp] = () => { var p = GetFocusedPane(); for (int i = 0; i < 5; i++) p.NewKeyDownEvent(Key.CursorUp); return true; },

            // Actions
            [AppCommand.AddPlaylist] = () => { _ = OnAddByUrlAsync(); return true; },
            [AppCommand.ToggleTrack] = () => { OnToggleTrack(); return true; },
            [AppCommand.ToggleAllTracking] = () => { OnToggleAllTracking(); return true; },
            [AppCommand.Sync] = () => { OnSync(); return true; },
            [AppCommand.SyncAll] = () => { OnSyncAll(); return true; },
            [AppCommand.Export] = () => { _ = OnExport(); return true; },
            [AppCommand.ShowHistory] = () => { _ = OnShowHistory(); return true; },
            [AppCommand.SortMenu] = () => { ShowSortMenu(); return true; },
            [AppCommand.Search] = () => { ShowSearch(); return true; },
            [AppCommand.ToggleDeleted] = () => { OnToggleDeleted(); return true; },
            [AppCommand.Settings] = () => { OnSettings(); return true; },
            [AppCommand.UpdateCheck] = () => { OnUpdateCheck(); return true; },
            [AppCommand.Help] = () => { global::Terminal.Gui.Application.Run(new HelpDialog()); return true; },
            [AppCommand.Quit] = () => { global::Terminal.Gui.Application.RequestStop(); return true; },

            // Profile-specific
            [AppCommand.NewProfile] = () => { OnNewProfile(); return true; },
            [AppCommand.ToggleLogin] = () => { OnToggleLogin(); return true; },
            [AppCommand.RenameProfile] = () => { OnRenameProfile(); return true; },
            [AppCommand.SetDefault] = () => { OnSetDefaultProfile(); return true; },
            [AppCommand.DeleteProfile] = () => { OnDeleteProfile(); return true; },
            [AppCommand.ProfileMenu] = () => { ShowProfileContextMenu(); return true; },
        };

        // Application.KeyDown fires BEFORE view hierarchy — required for
        // letter keys that child views (ListView/TableView) would consume.
        global::Terminal.Gui.Application.KeyDown += OnApplicationKeyDown;
    }

    private void CleanupCommands()
    {
        global::Terminal.Gui.Application.KeyDown -= OnApplicationKeyDown;
    }

    private void OnApplicationKeyDown(object? sender, Key key)
    {
        // Skip during modal dialogs
        if (!IsCurrentTop) return;

        // Skip when text input has focus (search field, dialog input)
        if (global::Terminal.Gui.Application.Navigation?.GetFocused() is TextField or TextView) return;

        // Profile-specific bindings (only when profile pane focused)
        if (_profileList.HasFocus
            && s_profileKeys.TryGetValue(key, out var profileCmd)
            && _commands.TryGetValue(profileCmd, out var profileHandler))
        {
            profileHandler();
            key.Handled = true;
            return;
        }

        // Global bindings
        if (s_globalKeys.TryGetValue(key, out var cmd)
            && _commands.TryGetValue(cmd, out var handler))
        {
            handler();
            key.Handled = true;
        }
    }

    protected override bool OnKeyDown(Key key)
    {
        // Ctrl+C double-press to quit (stateful, needs to pre-empt default Ctrl+C)
        if (key == Key.C.WithCtrl)
            return HandleCtrlC();

        return base.OnKeyDown(key);
    }

    private bool HandleCtrlC()
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

    private View GetFocusedPane() => (_profileList.HasFocus, _playlistList.HasFocus) switch
    {
        (true, _) => _profileList,
        (_, true) => _playlistList,
        _ => _videoTable,
    };

    private bool SwitchPane(int direction)
    {
        View[] panes = [_profileList, _playlistList, _videoTable];
        var current = Array.FindIndex(panes, p => p.HasFocus);
        var target = current + direction;
        if (target < 0 || target >= panes.Length) return false;
        panes[target].SetFocus();
        UpdateHintBar();
        return true;
    }

    private bool CyclePane(int direction)
    {
        View[] panes = [_profileList, _playlistList, _videoTable];
        var current = Array.FindIndex(panes, p => p.HasFocus);
        if (current < 0) current = 0;
        var next = direction > 0
            ? (current + 1) % panes.Length
            : (current - 1 + panes.Length) % panes.Length;
        panes[next].SetFocus();
        UpdateHintBar();
        return true;
    }
}
