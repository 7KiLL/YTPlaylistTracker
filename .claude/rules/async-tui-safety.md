# Async & Threading Safety for Terminal.Gui v2

## Application.Invoke() must NEVER receive an async lambda

`Application.Invoke()` accepts `Action`, not `Func<Task>`. Passing `async () => ...` compiles but the lambda becomes fire-and-forget -- exceptions are swallowed and execution order is unpredictable.

**Wrong:**
```csharp
Application.Invoke(async () => {
    var data = await _service.LoadAsync();  // fire-and-forget!
    _listView.SetSource(data);
});
```

**Correct:**
```csharp
Application.Invoke(() => {
    var data = _service.LoadAsync().GetAwaiter().GetResult();
    _listView.SetSource(data);
});
```

Using `.GetAwaiter().GetResult()` inside `Invoke` is safe because Terminal.Gui does not install a `SynchronizationContext`, so there is no deadlock risk from blocking.

## Background work pattern

Use `Task.Run` for long-running or async work. Use `Application.Invoke` only to marshal results back to the UI thread:

```csharp
Task.Run(async () => {
    var result = await _service.SyncAsync();
    Application.Invoke(() => {
        UpdateUI(result);
    });
});
```

Never access UI controls from inside `Task.Run` without wrapping in `Application.Invoke`.

> **v2 note**: The instance-based pattern uses `App?.Invoke()` (via `View.App`).
> Our codebase currently uses the static `Application.Invoke()` which delegates
> to the same underlying mechanism. Both are safe.

## Timer cleanup

Every call to `Application.AddTimeout` must have a corresponding `RemoveTimeout` path. If a method like `ShowSpinner()` can be called multiple times, it must remove/cancel the previous timer before creating a new one:

```csharp
private object? _spinnerToken;

void ShowSpinner() {
    if (_spinnerToken != null)
        Application.RemoveTimeout(_spinnerToken);
    _spinnerToken = Application.AddTimeout(TimeSpan.FromMilliseconds(200), () => {
        // update spinner frame
        return true; // return true to continue, false to stop
    });
}
```

> **v2 note**: `Application.AddTimeout` returns `object?` and its callback
> returns `bool` (true = continue, false = stop). The old `MainLoop.AddTimeout`
> pattern used `Func<MainLoop, bool>` — if you see that in older code, update it.

## Don't mix UI updates and async DB calls in one method

Keep UI-thread code and async/background code in separate methods or blocks. A single method that does both is a threading bug waiting to happen.
