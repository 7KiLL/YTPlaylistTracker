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
- B3: Replace `Application.Top` with `IsCurrentTop`, `Application.Navigation`, `SetNeedsDraw()`
- B4: Fix EF Core cross-context tracking conflict in single-playlist sync
- B5: Disable tracking for manual-only playlists (Liked Videos)
- Centralize UI status literals (`[x]`, `Active`, `Removed`, etc.) in `Glyphs.cs`
- V2: Command-based key bindings with `AppCommand` enum + dictionary dispatch
- V3: Replace custom spinner with `SpinnerView`

### v0.10.0
- Upgrade Terminal.Gui to 2.0.0-develop.5245 (breaking: namespace restructuring)
- V1: Migrate `Colors.ColorSchemes` → `SchemeManager` + `SchemeName` (25 sites)
- V4: Replace `ContextMenu` → `PopoverMenu` for profile context menu
- RadioGroup → `OptionSelector` (theme selector, glyph mode, export format)
- Fix breaking API renames: `CheckedState` → `Value`, `SelectedItemChanged` → `ValueChanged`, `MouseClick` → `MouseEvent`, `CursorPosition` → `InsertionPoint`, `ShadowStyle` → `ShadowStyles`, `Dim.Func` signature, nullable `SelectedItem`
- U1: Proportional pane widths with minimum floor
- Collapsible profile pane with auto-hide for single profile
- V5: Replace ad-hoc input dialogs with `Prompt<T>` API
- E1: E2E scenario test project with TUnit (20 tests: smoke, navigation, themes, features)

</details>

---

## Bugs & Anti-patterns

Issues that are wrong today and should be fixed.

### ~~B3. `Application.Top` static access~~ (fixed)

Replaced with `IsCurrentTop`, `Application.Navigation.GetFocused()`, and `SetNeedsDraw()` on `this`.

---

## v2 Alignment

Patterns that work today but use deprecated/v1 APIs. Not bugs, but tech debt.

### ~~V1. `Colors.ColorSchemes["..."]` dictionary access (25 sites)~~ (done)

Migrated to `SchemeManager.AddScheme()` + `View.SchemeName` string-based lookups. Custom schemes registered with `ytpt.*` prefix. `ColorScheme` type replaced by `Scheme`.

### ~~V2. Manual `KeyDown` event handling -> Command-based bindings~~ (done)

Replaced imperative if-chain with `AppCommand` enum + dictionary-based dispatch in `OnKeyDown`. Eliminated `Application.KeyDown` subscription and all three manual guards.

### ~~V4. `ContextMenu` -> `PopoverMenu`~~ (done)

Replaced `ContextMenu` with `PopoverMenu` + `Application.Popovers.Register()` in profile context menu.

### ~~V5. Ad-hoc input dialogs -> `Prompt<T>`~~ (done)

Replaced 3 manual Dialog+TextField+OK/Cancel patterns with `Dialogs.PromptForText()` helper wrapping `host.Prompt<TextField, string?>()`. Callsites: OnAddByUrlAsync, OnNewProfile, OnRenameProfile.

---

## New Features

### F1. Playlist diff view
Side-by-side before/after comparison when a sync detects changes, making removals easier to review at a glance.

### F2. Track video duration & view count
Fetch `contentDetails`/`statistics` from YouTube API and store in DB for richer historical records. No UI display needed initially.

---

## Polish & UX

### ~~U1. Responsive pane widths~~ (done)

Replaced fixed widths with `Dim.Func` percentage-based sizing with minimum floors.

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

### ~~E1. E2E scenario test project + harness~~ (done)

Created `YTPlaylistTracker.E2ETests` with TUnit (source-gen, avoids xunit/NUnit Terminal.Gui module init crash on .NET 10). AppHarness + AppHarnessBuilder + MockFactory + MainScreen screen object + entity builders. 20 tests across smoke, navigation, data binding, themes, and features.

### E2. Scenario catalog (in progress)

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

1. **U2** — Built-in scrollbars (now available in develop build)
2. **F1-F2** — New features as time permits
3. **I1** — Instance-based Application pattern (migrate off deprecated static API)
4. Everything else by category priority
