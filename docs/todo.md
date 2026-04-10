# TODO / Backlog

## Completed

<details>
<summary>v0.1.0 - v0.5.0 (click to expand)</summary>

### v0.1.0 - v0.4.0
- Multi-profile support, playlist tracking with soft-delete history
- OAuth2 sign-in (persisted credentials), lazy YouTube API init
- TUI three-pane layout (profiles, playlists, videos)
- Video search (title/channel), sorting (title, channel, date, status)
- Removed videos view, detail dialogs for profiles/playlists/videos
- Dark theme, Ctrl+C quit confirmation, database reset command
- Cross-platform browser launching, EF Core migrations
- Video metadata (AddedAt, Description, ThumbnailUrl, Position, JsonMetadata)
- Playlist metadata (Description, ThumbnailUrl, PublishedAt, JsonMetadata)
- Release infrastructure (GitHub Actions, 5-platform builds, install scripts)
- Fixture-based sync tests

### v0.4.x Bug Fixes
- Fix `Application.Invoke(async () => ...)` fire-and-forget — replaced with sync lambda + `.GetAwaiter().GetResult()`
- Fix `ShowSpinner()` timer leak — clean up previous timer before creating new one
- Fix spinner flicker — interval 80ms -> 200ms
- Fix `UserSettings` test flakiness — `File.Move` with `overwrite: true`
- Add `validate-secrets` CI job in release.yml
- Improve API logging with `[API]` prefix

### v0.5.0
- Auto-sync on startup (configurable)
- Removal history/timeline view
- Export removed videos report (CSV/JSON)
- Bulk import playlists from YouTube account

### v0.6.0 - v0.9.0
- Enrich profile with YouTube channel info (name, ID, avatar)
- Auto-update system (title bar notification, in-place binary replacement)
- Profile management in TUI (per-profile OAuth, CRUD dialogs, context menu, hotkeys)
- Profile pane reselection bug fix
- Video detail Status field formatting fix
- Settings dialog tabbed layout
- Glyph fallback for terminals without emoji support
- B1: Fix async lambda in `Accepting` handler
- B2: Fix stale docs referencing v1 APIs
- V3: Replace custom spinner with `SpinnerView`

</details>

---

## Bugs & Anti-patterns

Issues that are wrong today and should be fixed.

### B3. `Application.Top` static access (4 locations)
**See**: improvements.md R1

`Application.Top` is legacy in v2. Current usages:
- `MainWindow.KeyHandling.cs:23,27` — guard checks
- `MainWindow.Profile.cs:161` — `SetNeedsDraw()`
- `SettingsDialog.Storage.cs:71` — `RequestStop()`

Replace with `View.App?.TopRunnableView` and `Application.Navigation.GetFocused()` when upgrading Terminal.Gui.

---

## v2 Alignment

Patterns that work today but use deprecated/v1 APIs. Not bugs, but tech debt.

### V1. `Colors.ColorSchemes["..."]` dictionary access (20+ sites)
**See**: improvements.md R2

**Files**: `Theme.cs:47-50`, `MainWindow.Layout.cs`, `DetailDialog.cs`, `SettingsDialog.cs`, `WelcomeDialog.cs`, and more.

v2 replacement: `Scheme` / `SchemeManager` / `VisualRole`. The dictionary still works in 2.0.0 but is the v1 pattern.

### V2. Manual `KeyDown` event handling -> Command-based bindings
**See**: improvements.md P1

**File**: `MainWindow.KeyHandling.cs` (entire file)

Current: imperative `Application.KeyDown` with key comparisons and `key.Handled = true`.
Target: `AddCommand()` + `KeyBindings.Add()` for declarative, user-configurable bindings.

Benefits: JSON-configurable, self-documenting, no manual text-field guards.

### V4. `ContextMenu` -> `PopoverMenu`
**See**: improvements.md P3

**File**: `MainWindow.Profile.cs` (profile context menu)

v2's `PopoverMenu` provides richer cascading menus, better keyboard support, and consistent styling.

### V5. Ad-hoc input dialogs -> `Prompt<T>`
**See**: improvements.md P6

**Files**: `MainWindow.Actions.cs:14-32` (OnAddByUrlAsync), `MainWindow.Profile.cs` (rename dialog)

Several places build `Dialog` + `TextField` + OK/Cancel manually. v2's `Prompt<T>` does this in one call.

---

## New Features

### F1. Playlist diff view
Side-by-side before/after comparison when a sync detects changes, making removals easier to review at a glance.

### F2. Track video duration & view count
Fetch `contentDetails`/`statistics` from YouTube API and store in DB for richer historical records. No UI display needed initially.

---

## Polish & UX

### U1. Responsive pane widths
**See**: improvements.md G2

**File**: `MainWindow.Layout.cs:22,47`

Profile/playlist panes use fixed widths (`Width = 18`, `Width = 28`). Use `Dim.Auto()` or `Dim.Percent()` to adapt to terminal size.

### U2. Built-in scrollbars
**See**: improvements.md G1

Use `ViewportSettings.HasVerticalScrollBar` on scrollable views instead of manual scroll handling.

### U3. Mouse hover states
**See**: improvements.md G3

v2's `MouseHighlightStates` provides automatic hover/press visual feedback on interactive views.

### U4. Link view for URLs
**See**: improvements.md G5

**File**: `DetailDialog.cs` (browser buttons)

v2's built-in `Link` view for clickable hyperlinks. Could replace manual button + `ISystemLauncher.OpenUrl()` for YouTube links.

### U5. Tab-style borders for pane headers
**See**: improvements.md G8

v2's `BorderSettings.Tab` could replace interior header Labels inside FrameViews with integrated tab-style titles.

### U6. ConfigurationManager for user themes
**See**: improvements.md G4

**Files**: `Theme.cs`, `ThemePalette.cs`

Expose themes via `~/.tui/ytpt.config.json` for user customization without code changes.

---

## Testing

> **Deferred**: E2E tests planned for full Terminal.Gui v2 release.

### E1. E2E scenario test project + harness
**See**: improvements.md T1

Create `YTPlaylistTracker.ScenarioTests` with `TuiTestHarness` that initializes Terminal.Gui headlessly, mocks services via NSubstitute, and exposes helpers for seed data and key simulation.

### E2. Scenario catalog
**See**: improvements.md T2

Comprehensive scenario tables covering: initialization, keyboard navigation, data binding, sync, themes, column layout, and dialogs. Full catalog in `improvements.md`.

### E3. Virtual time for timer tests
**See**: improvements.md T4

Test spinner animation, search debounce, and auto-sync timers using `VirtualTimeProvider` (v2.x+) instead of real delays.

### E4. TestLogging integration
**See**: improvements.md T5

Use `TestLogging.BindTo(testOutputHelper)` to capture Terminal.Gui internal logs in test output for diagnosing failures.

---

## Infrastructure

### I1. Instance-based Application pattern (large refactor)
**See**: improvements.md P4

Move from static `Application.*` to `IApplication` instances for testability. Best done when upgrading to a newer Terminal.Gui version. Affects all UI files.

### I2. `IRunnable<TResult>` for dialogs
**See**: improvements.md P5

**Files**: `WelcomeDialog.cs`, `SettingsDialog.cs`, `DetailDialog.cs`, etc.

Type-safe modal results instead of manual variable capture. Requires v2.x+ API.

### I3. Smoke tests with Testcontainers
End-to-end test in a Linux container to catch platform-specific issues in CI.

### I4. Logging integration
**See**: improvements.md G6

v2's `Logging` class is compatible with `Microsoft.Extensions.Logging`. Could unify app logging with Terminal.Gui internal logging.

---

## Priority Order

1. **B3** — Application.Top migration (3 locations, minimal effort)
2. **V2** — Command-based key bindings (declarative, configurable)
3. **V4** — PopoverMenu for context menus
4. **V1** — Colors.ColorSchemes migration (20+ sites, batch refactor)
5. **V5** — Prompt\<T\> for input dialogs
6. **U1** — Responsive pane widths
7. **F1-F2** — New features as time permits
8. Everything else by category priority
