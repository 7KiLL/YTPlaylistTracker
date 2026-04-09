# Multitasking & Threading

## Core Rule

**All UI operations must happen on the main thread.** Marshal updates via `Application.Invoke()` (or `App?.Invoke()` in v2).

## Recommended Patterns

### Async/Await with Task.Run

```csharp
Task.Run(async () =>
{
    var result = await _service.SyncAsync();
    Application.Invoke(() =>
    {
        UpdateUI(result);  // safe — on main thread
    });
});
```

### Blocking Inside Invoke

`.GetAwaiter().GetResult()` is safe inside `Invoke()` because Terminal.Gui has no `SynchronizationContext`:

```csharp
Application.Invoke(() =>
{
    var data = _service.LoadAsync().GetAwaiter().GetResult();
    _listView.SetSource(data);
});
```

## Timer Management

```csharp
private object? _timerToken;

void StartTimer()
{
    // ALWAYS clean up previous timer first
    if (_timerToken != null)
        Application.RemoveTimeout(_timerToken);

    _timerToken = Application.AddTimeout(
        TimeSpan.FromMilliseconds(200),
        _ => {
            // return true to continue, false to stop
            return _isActive;
        });
}
```

**Critical**: Every `AddTimeout` must have a corresponding `RemoveTimeout` path.

## Progress & Cancellation

```csharp
var cts = new CancellationTokenSource();

Task.Run(async () =>
{
    for (int i = 0; i < 100 && !cts.Token.IsCancellationRequested; i++)
    {
        await DoWorkAsync(cts.Token);
        Application.Invoke(() => progressBar.Fraction = i / 100f);
    }
});

// Cancel on user action
cancelBtn.Accepting += (s, e) => cts.Cancel();
```

## Common Pitfalls

| Pitfall | Fix |
|---------|-----|
| UI access from `Task.Run` | Wrap in `Application.Invoke()` |
| `async` lambda in `Invoke()` | Use `.GetAwaiter().GetResult()` instead |
| Forgotten timer cleanup | Always track token, remove in dispose |
| `.Result` / `.Wait()` on UI thread | Use `Task.Run` + `Invoke` pattern |

## v2 Instance-Based Pattern

v2 recommends `App?.Invoke()` over static `Application.Invoke()`. The static version still works but the instance pattern improves testability.

## ytpt Threading Pattern

- Background sync via `Task.Run()` + `Application.Invoke()` for UI updates
- Spinner animation via `AddTimeout` with proper cleanup
- Search debounce via timer with cleanup before new timer
- All async safety rules enforced via `.claude/rules/async-tui-safety.md`
