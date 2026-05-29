# Terminal.Gui 2.0.1 → 2.4.3 migration notes

Working notes for the upgrade on branch `chore/terminal-gui-2.4.3`. Delete after merge or fold into README.

## Hard compile breaks (done)
- `TableSelection.Cursor` removed → use `TableSelection.SelectedCell` (a `Point`; `.Y` = row).
  Sites: `MainWindow.Actions.cs`, `RemovalHistoryDialog.cs`, E2E `Screens/MainScreen.cs`.

## Deprecation: static `Application` "is going away" (CS0618)
Migrate to instance `IApplication`. Replacements:

| Deprecated static                 | New instance call            |
|-----------------------------------|------------------------------|
| `Application.Init(driver)`         | `app.Init(driver)`           |
| `Application.Create()`             | (factory — NOT deprecated)   |
| `Application.Run(view)`            | `app.Run(view)`              |
| `Application.RequestStop()`        | `app.RequestStop()`          |
| `Application.Invoke(a)`            | `app.Invoke(a)`              |
| `Application.AddTimeout(t,f)`      | `app.AddTimeout(t,f)`        |
| `Application.RemoveTimeout(o)`     | `app.RemoveTimeout(o)`       |
| `Application.Shutdown()`           | `app.Dispose()` (IApplication : IDisposable) |
| `Application.Driver`               | `app.Driver`                 |
| `Application.Navigation`           | `app.Navigation`             |
| `Application.Popovers`             | `app.Popovers`               |
| `Application.KeyDown += h`         | `app.Keyboard.KeyDown += h`  |
| `Application.RaiseKeyDownEvent(k)` | `app.Keyboard.RaiseKeyDownEvent(k)` |
| `Application.StopAfterFirstIteration` | `app.StopAfterFirstIteration` |
| `Application.Initialized`          | `app.Initialized`            |
| `Application.LayoutAndDraw()`      | `app.LayoutAndDraw()`        |

`TextView` is also `[Obsolete]` (superseded by gui-cs/Editor). We keep it (read-only detail/log views); suppress locally — adding the Editor package is out of scope.

## App-reference threading (the key decision)
- `View.App` is **null before `Run`** and is set on the top-most SuperView during `Run`.
- `MainWindow.InitializeAsync` schedules `AddTimeout` **before** `Run`, so it cannot use `App`.
- Solution: `MainWindow` holds `private IApplication _app` set at the **start** of `InitializeAsync(IApplication app)`. Use `_app.*` everywhere in MainWindow (valid pre-Run, during Run, and for the KeyDown subscription).
- Bootstrap (`CliCommands.RunUi`, test `AppHarness`): create one `IApplication app = Application.Create(); app.Init(...);`, call `InitializeAsync(app)`, `app.Run(window)`, dispose (`using`).
- Modal dialogs created+run from a view (`SettingsDialog`, `WelcomeDialog`, `HelpDialog`, `RemovalHistoryDialog`, `RemovedVideosDialog`, `DetailDialog`): their handlers fire **while running**, so the inherited `App` is non-null → use `App!.RequestStop()` / `App!.Run(...)`. For app-level subscriptions inside a dialog (SettingsDialog `KeyDown`/`Navigation`), wire on the `Initialized` event and unsubscribe on dispose.
- `Dialogs` static helper: add `IApplication app` first parameter to `Query`; callers pass `_app` (from MainWindow) or `App!` (from a running dialog). `PromptForText` already takes an `IRunnable host` and uses `host.Prompt<>` — no static Application use, leave as is.

## KeyDown wiring (prior rollback area — handle with care)
- App-level `KeyDown` intentionally pre-empts ListView type-ahead.
- New: `_app.Keyboard.KeyDown += OnApplicationKeyDown` in `RegisterCommands` (runs after `_app` set), unsubscribe in `CleanupCommands`.
- `DispatchKey`: `Application.Navigation?.GetFocused()` → `_app.Navigation?.GetFocused()`.
- `OnKeyDown` override stays the fallback. E2E nav/search tests are the safety net.

## Tests / harness
- `AppHarness`: `using`-owned `IApplication`; `app.Init(DriverRegistry.Names.ANSI)`; `app.Driver!.SetScreenSize(...)`; `app.StopAfterFirstIteration = true; app.Run(window)`; dispose via `app.Dispose()`.
- `ScreenCapture.Capture(IApplication app)`: `app.LayoutAndDraw(); app.Driver!.ToString()`.
- `AppBootTests`: `using IApplication app = Application.Create(); app.Init(...); app.Initialized; app.Dispose()`.

## Contracts (Verify snapshots)
Rendering may differ 2.0.1→2.4.3 (borders/scrollbars/glyphs/default schemes). Run E2E snapshot tests; for each `*.received.txt` diff, classify legit-render-change vs regression before accepting the new `*.verified.txt` baseline.

Outcome (all 4 LayoutSnapshotTests):
- `EmptyState_DefaultLayout`, `ProfilePaneHidden` — matched 2.0.1 baselines exactly, no change.
- `WithPlaylistsAndVideos` — one title now renders `Never Gonna Give Yo…` (was `Never Gonna Give You`). Benign: TG 2.4.3 TableView signals over-wide cell clipping with a `…` glyph; column geometry unchanged. Baseline updated.
- `WithDeletedVideos` — old baseline (`Videos [3]`) was a race artifact: the test presses F8 (show-removed) but the toggle's async refresh didn't land before capture, and the builder never mocked `GetDeletedVideosAsync`. Fixed `AppHarnessBuilder` to mock the deleted subset; baseline now correctly shows `Removed [2]`.

## Test harness (instance app)
`StopAfterFirstIteration + Run` ends the session on the instance app, leaving the draw buffer empty. Use `app.Begin(window)` (held for the harness lifetime, `app.End(token)` on dispose) so the window stays the active runnable for `ScreenCapture`. `ScreenCapture.Capture(app)` calls `app.LayoutAndDraw(true)` (forceRedraw for deterministic capture).

## Post-migration fixes / follow-ups
- **SettingsDialog KeyDown leak (fixed):** it unsubscribes its app-level `Keyboard.KeyDown` on `Disposing`, but `OnSettings` ran it via `_app.Run(...)` without disposing (TG `Run` never disposes the runnable). `OnSettings` now disposes the dialog on the UI thread right after `Run` returns (before the `ConfigureAwait(false)` await), so the handler is released. TG v2 rule: caller-created runnables must be disposed.
- **Layout-timing quirk (pre-existing, not fixed):** the initial background video-load path and the F8 refresh path compute `ColumnLayout` from different `_videoTable.Viewport.Width` values, so Title-column width can differ between screens. Not introduced by this upgrade; candidate for a layout-settle before the initial `ApplyFilterAndSort`.

## Rule docs needing a follow-up edit (blocked by self-modification guard)
`.claude/rules/terminal-gui-v2-patterns.md` ("Static Application API (Current)") and `.claude/rules/async-tui-safety.md` (v2 note) still describe the codebase as using the **static** API. That is now false — they should be updated to the instance `IApplication` pattern. Editing `.claude/rules/*` was auto-denied as agent self-modification, so it needs the user's go-ahead.
