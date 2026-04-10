# V2 Command-Based Key Bindings Migration

## Problem

`MainWindow.KeyHandling.cs` uses a 130-line imperative if-chain in `Application.KeyDown` (fires before views) with three manual guards:
- `IsCurrentTop` — skip during modal dialogs
- `Navigation.GetFocused() is TextField` — skip during text input
- `_searchField.HasFocus` — skip during search

This pattern is fragile, not configurable, and doesn't align with Terminal.Gui v2's command-based input system.

**Previous attempt was rolled back** because keys stopped working entirely — likely caused by using `View.KeyBindings.Add` on MainWindow for letter keys, where focused child views consumed them before MainWindow's KeyBindings fired.

## Key Insight: v2 Event Flow

```
MainWindow.NewKeyDownEvent(key)
  ├─ Recurse into focused child (ListView/TableView)
  │   ├─ child.OnKeyDown → child.KeyBindings
  │   └─ If child doesn't handle key → returns false
  ├─ MainWindow.OnKeyDown ← OUR DISPATCH POINT
  ├─ MainWindow.KeyBindings
  └─ MainWindow.HotKeyBindings
```

- **ListView** does NOT bind letter keys (only CursorUp/Down, Enter, Space). Letters propagate to parent.
- **TextField** DOES bind all letters (text input). Letters are consumed — never reach parent.
- **Modal dialogs** become the active Toplevel. MainWindow doesn't receive keys at all.

This means: **all three manual guards become unnecessary.** The v2 key routing handles them automatically when we use `OnKeyDown` on MainWindow instead of `Application.KeyDown`.

## Why NOT use Terminal.Gui's KeyBindings.Add

`Command` is a closed enum (~60 values). We have ~20 custom actions (Sync, SyncAll, Export, ShowHistory, etc.) with no semantic match. Force-fitting (`Command.Redo` = "update check") makes code misleading and fragile. `AddCommand` also replaces built-in handlers, risking breakage.

## Design

### Custom AppCommand Enum

```csharp
// New file: src/YTPlaylistTracker.UI/AppCommand.cs
internal enum AppCommand
{
    // Global actions
    AddPlaylist, ToggleTrack, ToggleAllTracking,
    Sync, SyncAll, Export, ShowHistory,
    SortMenu, Search, ToggleDeleted,
    Settings, UpdateCheck, Help, Quit,

    // Navigation
    PaneLeft, PaneRight, PaneNext, PanePrev,
    NavUp, NavDown, FastUp, FastDown,

    // Profile-specific (only when profile pane focused)
    NewProfile, ToggleLogin, RenameProfile,
    SetDefault, DeleteProfile, ProfileMenu,
}
```

### Key Binding Maps (data-driven)

```csharp
// In MainWindow.KeyHandling.cs
private static readonly Dictionary<Key, AppCommand> s_globalKeys = new()
{
    // Vim navigation
    [(Key)'h'] = AppCommand.PaneLeft,
    [(Key)'l'] = AppCommand.PaneRight,
    [(Key)'j'] = AppCommand.NavDown,
    [(Key)'k'] = AppCommand.NavUp,
    [(Key)'J'] = AppCommand.FastDown,
    [(Key)'K'] = AppCommand.FastUp,

    // Arrow pane switching
    [Key.CursorLeft] = AppCommand.PaneLeft,
    [Key.CursorRight] = AppCommand.PaneRight,

    // Fast scroll (shift+arrow)
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

    // F-keys (aliases)
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

    // Tab pane cycling
    [Key.Tab] = AppCommand.PaneNext,
    [Key.Tab.WithShift] = AppCommand.PanePrev,

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
```

### Command Handler Registration

```csharp
private Dictionary<AppCommand, Func<bool>> _commandHandlers = null!;

private void RegisterCommands()
{
    _commandHandlers = new()
    {
        [AppCommand.AddPlaylist] = () => { _ = OnAddByUrlAsync(); return true; },
        [AppCommand.ToggleTrack] = () => { OnToggleTrack(); return true; },
        [AppCommand.Sync] = () => { OnSync(); return true; },
        [AppCommand.Quit] = () => { Application.RequestStop(); return true; },
        // ... all handlers
        [AppCommand.NavDown] = () => { GetFocusedPane().NewKeyDownEvent(Key.CursorDown); return true; },
        [AppCommand.NavUp] = () => { GetFocusedPane().NewKeyDownEvent(Key.CursorUp); return true; },
        [AppCommand.FastDown] = () => { var p = GetFocusedPane(); for (int i = 0; i < 5; i++) p.NewKeyDownEvent(Key.CursorDown); return true; },
        // ... etc
    };
}
```

### Dispatch in OnKeyDown

```csharp
protected override bool OnKeyDown(Key key)
{
    // Ctrl+C double-press (special stateful logic)
    if (key == Key.C.WithCtrl)
        return HandleCtrlC();

    // Profile-specific bindings (only when profile list focused)
    if (_profileList.HasFocus
        && s_profileKeys.TryGetValue(key, out var profileCmd)
        && _commandHandlers.TryGetValue(profileCmd, out var profileHandler))
        return profileHandler();

    // Global bindings
    if (s_globalKeys.TryGetValue(key, out var cmd)
        && _commandHandlers.TryGetValue(cmd, out var handler))
        return handler();

    return base.OnKeyDown(key);
}
```

### Arrow Key Fix for TableView

TableView binds CursorLeft/Right for column navigation. Remove those bindings so arrows propagate to MainWindow for pane switching:

```csharp
// In SetupKeyHandling() or layout setup
_videoTable.KeyBindings.Remove(Key.CursorLeft);
_videoTable.KeyBindings.Remove(Key.CursorRight);
```

### What Gets Removed

- `Application.KeyDown += OnApplicationKeyDown` subscription — **deleted entirely**
- `OnApplicationKeyDown` method (40 lines of if-chain + 3 guards) — **deleted**
- `SetupKeyHandling()` / `CleanupKeyHandling()` — simplified to `RegisterCommands()`
- `IsCurrentTop` guard — unnecessary (v2 routing)
- `Navigation.GetFocused() is TextField` guard — unnecessary (TextField consumes letters)
- `_searchField.HasFocus` guard — unnecessary (same reason)

### Files Modified

| File | Change |
|------|--------|
| `AppCommand.cs` (new) | Enum definition |
| `MainWindow.KeyHandling.cs` | Full rewrite: dictionaries + OnKeyDown dispatch |
| `MainWindow.cs` | Remove SetupKeyHandling/CleanupKeyHandling calls |
| `MainWindow.Layout.cs` | Remove CursorLeft/Right from _videoTable |

### File Size Check

Current `MainWindow.KeyHandling.cs`: 198 lines. New version estimate: ~160 lines (dictionaries are compact). Within 300-line limit.

## Verification

1. `dotnet build` — compiles
2. `dotnet test` — all 100 tests pass
3. Manual test checklist:
   - [ ] h/l/arrows switch panes from all three panes
   - [ ] j/k navigate lists, J/K fast scroll
   - [ ] Shift+arrows fast scroll
   - [ ] Tab/Shift+Tab cycle panes
   - [ ] Letter keys (a/s/t/e/etc.) fire actions
   - [ ] F-keys fire same actions
   - [ ] Ctrl+C double-press quits
   - [ ] Ctrl+U checks update
   - [ ] Profile keys (n/L/r/d/x/Enter) only work with profile pane focused
   - [ ] Search field: typing doesn't trigger hotkeys
   - [ ] Modal dialogs: hotkeys don't fire through dialogs
   - [ ] q quits, ? opens help
