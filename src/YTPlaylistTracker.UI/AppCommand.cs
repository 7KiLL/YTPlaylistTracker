namespace YTPlaylistTracker.UI;

/// <summary>
/// Named commands for key binding dispatch. Data-driven alternative to
/// Terminal.Gui's closed Command enum, covering all ytpt-specific actions.
/// </summary>
internal enum AppCommand
{
    // Global actions
    AddPlaylist,
    ToggleTrack,
    ToggleAllTracking,
    Sync,
    SyncAll,
    Export,
    ShowHistory,
    SortMenu,
    Search,
    ToggleDeleted,
    Settings,
    UpdateCheck,
    Help,
    Quit,

    ToggleProfilePane,

    // Pane navigation
    PaneLeft,
    PaneRight,
    PaneNext,
    PanePrev,

    // List navigation (vim-style)
    NavUp,
    NavDown,
    FastUp,
    FastDown,

    // Profile-specific (only when profile pane focused)
    NewProfile,
    ToggleLogin,
    RenameProfile,
    SetDefault,
    DeleteProfile,
    ProfileMenu,
}
