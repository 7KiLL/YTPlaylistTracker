# Terminal.Gui Improvements

Findings from cross-referencing the v2 documentation against the ytpt codebase.
Categorized as **Required** (bugs/anti-patterns), **Preferred** (quality wins), and **Good-to-have** (polish).

> Project uses Terminal.Gui **2.0.0**. Some v2 features mentioned here may require a newer version — marked with (v2.x+).

---

## Required

### R1. `Application.Top` static access

**Files**: `MainWindow.KeyHandling.cs:23,27`, `MainWindow.Profile.cs:161`, `SettingsDialog.cs:67`

v2 docs say `Application.Top` is legacy. Preferred pattern:
- For checking if "this" is active: compare against `View.App?.TopRunnableView`
- For accessing most-focused view: `Application.Navigation.GetFocused()` instead of `Application.Top?.MostFocused`

```csharp
// Before
if (global::Terminal.Gui.Application.Top != this) return;
if (global::Terminal.Gui.Application.Top?.MostFocused is TextField) return;

// After (v2.x+)
if (View.App?.TopRunnableView != this) return;
if (Application.Navigation.GetFocused() is TextField) return;
```

### R2. `Colors.ColorSchemes["Base"]` dictionary access

**Files**: `MainWindow.Layout.cs:42,69`, `Theme.cs:47-50`

v2 introduces `Scheme` / `SchemeManager` / `VisualRole` as the replacement. The `Colors.ColorSchemes` dictionary still works in 2.0.0 but is the v1 pattern.

### R3. Update rules to match actual API usage

**File**: `.claude/rules/async-tui-safety.md`

Rules reference `Application.MainLoop.Invoke()` and `Application.MainLoop.AddTimeout()`, but the actual codebase uses `Application.Invoke()` and `Application.AddTimeout()` — the v2 static convenience methods. Rules should match what we actually use.

---

## Preferred

### P1. Command-based key bindings

**File**: `MainWindow.KeyHandling.cs` (entire file)

Current pattern: manual `Application.KeyDown` event with key comparisons and `key.Handled = true`. v2 pattern: `AddCommand()` + `KeyBindings.Add()` for declarative, user-configurable bindings.

```csharp
// Before (imperative)
Application.KeyDown += (s, key) => {
    if (key == (Key)'s') { OnSync(); key.Handled = true; }
};

// After (declarative)
AddCommand(Command.New, () => { OnSync(); return true; });
KeyBindings.Add((Key)'s', Command.New);
```

Benefits: configurable via JSON config, self-documenting, no manual guard against text fields.

### P2. Replace custom spinner with SpinnerView

**Files**: `MainWindow.cs:41` (SpinnerFrames), `MainWindow.Layout.cs:160-180` (ShowSpinner/HideSpinner)

v2 provides built-in `SpinnerView` with multiple animation styles. Eliminates custom timer management.

```csharp
// Before: manual spinner frames + AddTimeout
private static readonly string[] SpinnerFrames = ["...", "..."];
_spinnerTimer = Application.AddTimeout(TimeSpan.FromMilliseconds(200), () => { ... });

// After: built-in SpinnerView
var spinner = new SpinnerView { Visible = true };
```

### P3. Replace ContextMenu with PopoverMenu

**File**: `MainWindow.Profile.cs` (profile context menu)

v2's `PopoverMenu` provides richer cascading menus, better keyboard support, and consistent styling. `ContextMenu` still works but `PopoverMenu` is the v2 way.

### P4. Instance-based Application pattern (v2.x+)

**All UI files**

Move from static `Application.*` to `IApplication` instances for testability:
```csharp
// Before
Application.Init(); Application.Run(window); Application.Shutdown();

// After
using IApplication app = Application.Create().Init();
app.Run(window);
```

This is a large refactor best done when upgrading to a newer Terminal.Gui version that fully supports `IApplication`.

### P5. IRunnable<TResult> for dialogs (v2.x+)

**Files**: `WelcomeDialog.cs`, `SettingsDialog.cs`, `DetailDialog.cs`, etc.

Type-safe modal results instead of manual variable capture:
```csharp
// Before
string? inputValue = null;
okBtn.Accepting += (s, e) => { inputValue = input.Text; RequestStop(); };
Application.Run(dialog);

// After
app.Run<AddPlaylistDialog>();
string? url = app.GetResult<string>();
```

### P6. Use Prompt<T> for simple input dialogs

**Files**: `MainWindow.Actions.cs:14-32` (OnAddByUrlAsync), `MainWindow.Profile.cs` (rename dialog)

Several places build ad-hoc `Dialog` + `TextField` + OK/Cancel. v2's `Prompt<T>` does this in one call:
```csharp
string? url = app.Prompt(new TextField { Text = "" }, title: "Add Playlist");
```

---

## Good-to-Have

### G1. Built-in scrollbar on views

Instead of manual scroll handling, use `ViewportSettings.HasVerticalScrollBar` on views that scroll.

### G2. Dim.Auto for responsive layout

**File**: `MainWindow.Layout.cs:22,47`

Profile/playlist panes use fixed widths (`Width = 18`, `Width = 28`). `Dim.Auto()` or `Dim.Percent()` could make them responsive to terminal size.

### G3. Mouse highlight states

v2's `MouseHighlightStates` provides automatic hover/press visual feedback on interactive views. Could enhance list items and buttons.

### G4. ConfigurationManager for user themes

**Files**: `Theme.cs`, `ThemePalette.cs`

Instead of only hardcoded theme palettes, expose themes via `~/.tui/ytpt.config.json` for user customization without code changes.

### G5. Link view for URLs

**File**: `DetailDialog.cs` (browser buttons)

v2 has a built-in `Link` view for clickable hyperlinks. Could replace manual button + `ISystemLauncher.OpenUrl()` pattern for YouTube links.

### G6. Logging integration

v2's `Logging` class is compatible with `Microsoft.Extensions.Logging`. Could unify app logging with Terminal.Gui internal logging for debugging rendering/input issues.

### G7. Input injection for E2E testing

v2 provides `VirtualTimeProvider` + `app.InjectKey()` / `app.InjectMouse()` for deterministic TUI testing. Could enable scenario tests for add/sync/navigate workflows.

### G8. Tab-style borders for pane headers

v2's `BorderSettings.Tab` could replace the current header Labels inside FrameViews with integrated tab-style titles that visually connect to the border.

---

## Testing Improvements

### T1. E2E Scenario Test Project

Create `YTPlaylistTracker.ScenarioTests` with a `TuiTestHarness` that:
- Initializes Terminal.Gui headlessly (FakeDriver or ANSI driver)
- Mocks all services (repos, YouTube API, system launcher, etc.) via NSubstitute
- Exposes `AddPlaylist()`, `AddVideo()`, `AddProfile()` helpers for seed data
- Provides `SendKey(Key)` to simulate keyboard input
- Cleans up via `IDisposable` (disposes Window + `Application.Shutdown()`)

### T2. Scenario Catalog

#### Initialization Scenarios

| Scenario | Setup | Verify |
|----------|-------|--------|
| Empty state | No playlists | Window title shows "ytpt", default profile created |
| With playlists | 2 playlists, 5 videos | `GetByProfileAsync` called for default profile |
| With deleted videos | Mix of active + deleted | `GetVideosAsync` returns only active |
| Multiple profiles | 3 profiles, one default | Default profile's playlists load first |
| Auto-sync disabled | `AutoSyncOnStartup = false` | `SyncPlaylistAsync` NOT called |
| Auto-sync enabled | `AutoSyncOnStartup = true`, tracked playlist | Sync triggered on startup |

#### Keyboard Navigation Scenarios

| Scenario | Input | Verify |
|----------|-------|--------|
| Vim right | `l` key | Focus moves from profiles → playlists → videos |
| Vim left | `h` key | Focus moves from videos → playlists → profiles |
| Arrow right | `CursorRight` | Same as `l` |
| Arrow left | `CursorLeft` | Same as `h` |
| Fast scroll down | `Shift+CursorDown` | 5 rows scrolled at once |
| Fast scroll up | `Shift+CursorUp` | 5 rows scrolled at once |
| Quit | `q` key | `Application.RequestStop()` called |
| Double Ctrl+C | `Ctrl+C` twice < 2s | Application exits |
| Help | `F1` or `?` | Help dialog opens |

#### Data Binding Scenarios

| Scenario | Setup | Input | Verify |
|----------|-------|-------|--------|
| Select profile | 2 profiles | Navigate to profile 2, press Enter | `GetByProfileAsync(profile2.Id)` called |
| Select playlist | 3 playlists | Navigate to playlist 2, press Enter | `GetVideosAsync(playlist2.Id)` called |
| Toggle deleted | Videos + deleted videos | Press `d` | Deleted videos shown/hidden |
| Search | 10 videos with varied titles | Press `/`, type "music" | Only matching videos displayed |
| Sort by title | Videos with titles A-Z | Press `o` for sort options | Videos reordered |
| Empty playlist | Playlist with no videos | Select it | No crash, empty table |
| 100 videos | Playlist with 100 items | Select it | All load, scrollable |

#### Sync Scenarios

| Scenario | Setup | Input | Verify |
|----------|-------|-------|--------|
| Sync single | Selected tracked playlist | Press `s` | `SyncPlaylistAsync` called for selected |
| Sync all | Multiple tracked playlists | Press `S` | `SyncPlaylistAsync` called for each tracked |
| Sync adds video | API returns new video | Sync | Video appears in list |
| Sync removes video | API missing video | Sync | Video marked with `DeletedAt` |
| Sync error | API throws HttpRequestException | Sync | Error message shown, no crash |
| Sync progress | Long sync | During sync | Spinner visible with message |

#### Theme Scenarios

| Scenario | Setup | Verify |
|----------|-------|--------|
| Catppuccin | `ThemeName = "Catppuccin"` | `Theme.CurrentName == "Catppuccin"` |
| Dracula | `ThemeName = "Dracula"` | `Theme.CurrentName == "Dracula"` |
| Gruvbox Dark | `ThemeName = "Gruvbox Dark"` | `Theme.CurrentName == "Gruvbox Dark"` |
| Nord | `ThemeName = "Nord"` | `Theme.CurrentName == "Nord"` |
| High Contrast Dark | `ThemeName = "High Contrast Dark"` | All scheme properties set |
| High Contrast Light | `ThemeName = "High Contrast Light"` | All scheme properties set |
| Unknown theme | `ThemeName = "Nonexistent"` | Falls back to default palette |
| Theme sets globals | Any theme | `Dialog.DefaultShadow == None`, `Dialog.DefaultBorderStyle == Rounded` |
| All color schemes | Any theme | `Colors.ColorSchemes["Base/Dialog/Menu/Error"]` all populated |

#### Column Layout Scenarios

| Scenario | Input | Verify |
|----------|-------|--------|
| Narrow terminal (40) | `ColumnLayout.Compute(40)` | Title >= 20 (minimum) |
| Wide terminal (200) | `ColumnLayout.Compute(200)` | Title gets extra space |
| Exact fixed total (56) | `ColumnLayout.Compute(56)` | Title == 20 exactly |
| Various widths | 30, 50, 80, 120, 200, 300 | Fixed columns constant (4, 22, 12, 10) |

#### Dialog Scenarios

| Scenario | Input | Verify |
|----------|-------|--------|
| Add playlist by URL | Press `a`, enter URL, press Enter | `PlaylistRepo.AddAsync` called |
| Add playlist cancel | Press `a`, press Cancel | No repo call |
| Detail dialog | Select video, press Enter | Dialog shows video metadata |
| Settings dialog | Press `Ctrl+,` or settings key | Settings dialog opens |
| Export dialog | Press `e` | Export dialog opens with format options |
| Removed videos | Press `r` | Removed videos dialog shows |

### T3. Test Infrastructure Details

**Project**: `YTPlaylistTracker.ScenarioTests`

**Dependencies**: Terminal.Gui 2.0.0, NSubstitute, xunit, UI project reference

**InternalsVisibleTo**: Add to UI `.csproj` for access to `ColumnLayout`, `Theme`, etc.

**Pattern**:
```csharp
public class SomeTests : IDisposable
{
    private TuiTestHarness? _harness;

    [Fact]
    public async Task Scenario_Description()
    {
        _harness = new TuiTestHarness();
        _harness.AddPlaylist("PL1", "Favorites");
        _harness.AddVideo(playlist, "v1", "Cool Video");
        await _harness.InitializeAsync();

        _harness.SendKey((Key)'s');  // trigger sync

        await _harness.SyncService.Received(1)
            .SyncPlaylistAsync(Arg.Any<Playlist>(), ...);
    }

    public void Dispose() => _harness?.Dispose();
}
```

### T4. Virtual time for timer-dependent tests

Test spinner animation, search debounce, and auto-sync timers using `VirtualTimeProvider` (v2.x+) instead of real delays.

### T5. TestLogging for debugging

Use `TestLogging.BindTo(testOutputHelper)` to capture Terminal.Gui internal logs in test output for diagnosing failures.

---

## Priority Order

1. **R3** — Fix rules to match reality (minimal effort, high value)
2. **T1-T3** — E2E scenario test infrastructure + scenarios
3. **P2** — Replace custom spinner (eliminates timer complexity)
4. **P1** — Command-based key bindings (declarative, configurable)
5. **R1** — Application.Top migration (when upgrading Terminal.Gui)
6. **P3** — PopoverMenu (richer context menus)
7. Everything else as time permits
