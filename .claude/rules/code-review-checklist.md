# Code Review Checklist

Check each item before approving changes.

## Async/Await Patterns
- [ ] No `async void` methods (except UI event handlers like `Application.Run` callbacks)
- [ ] No fire-and-forget `Task` calls -- all tasks are awaited or explicitly tracked
- [ ] No `async` lambdas passed to `Application.Invoke()` (see async-tui-safety.md)
- [ ] No `.Result` or `.Wait()` on tasks in contexts with a SynchronizationContext (inside `Application.Invoke` is fine since Terminal.Gui has none)

## Timer & Resource Cleanup
- [ ] Every `AddTimeout` has a corresponding `RemoveTimeout` path
- [ ] Repeated calls to show/hide patterns clean up previous state first
- [ ] Animation intervals are >= 150ms to avoid flicker
- [ ] `IDisposable` resources are disposed (DbContext, HttpClient, etc.)

## Terminal.Gui Threading
- [ ] All UI control updates happen on the UI thread (inside `Application.Invoke` or direct event handlers)
- [ ] No direct UI access from `Task.Run` or background threads
- [ ] Long-running work is offloaded to `Task.Run`, not run on the UI thread

## Terminal.Gui v2 Patterns
- [ ] No `Application.Top` -- use `View.App?.TopRunnableView` or local references
- [ ] No `Colors.ColorSchemes["name"]` -- use `Scheme` / `SchemeManager` when available
- [ ] Key handling uses `AddCommand()` + `KeyBindings` where possible, not raw `KeyDown` events
- [ ] Buttons/dialogs use `Accepting` event (not deprecated `Clicked`)
- [ ] Modal dialogs call `Application.RequestStop()` to close (not `Toplevel.RequestStop()`)
- [ ] Views set `CanFocus = true` explicitly when needed (v2 defaults to false)

## Test Robustness
- [ ] File operations use `overwrite: true` where the destination may exist (e.g., `File.Move`, `File.Copy`)
- [ ] Tests clean up temp files/directories in `finally` or `Dispose`
- [ ] Tests don't depend on execution order or global state
- [ ] Async tests use `await` (not `.Result` or `.Wait()`)
- [ ] TUI scenario tests use `VirtualTimeProvider` + ANSI driver for determinism

## General
- [ ] No secrets or credentials in source code (use build-time injection or env vars)
- [ ] Log messages have a clear prefix/category (e.g., `[API]`, `[Sync]`, `[DB]`)
- [ ] Error messages shown to users are actionable, not stack traces
