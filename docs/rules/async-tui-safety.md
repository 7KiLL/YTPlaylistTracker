# Async & Threading Safety for Terminal.Gui

## MainLoop.Invoke() must NEVER receive an async lambda

Terminal.Gui's `MainLoop.Invoke()` accepts `Action`, not `Func<Task>`. Passing `async () => ...` compiles but the lambda becomes fire-and-forget -- exceptions are swallowed and execution order is unpredictable, causing deadlocks.

**Wrong:**
```csharp
Application.MainLoop.Invoke(async () => {
    var data = await _service.LoadAsync();  // fire-and-forget!
    _listView.SetSource(data);
});
```

**Correct:**
```csharp
Application.MainLoop.Invoke(() => {
    var data = _service.LoadAsync().GetAwaiter().GetResult();
    _listView.SetSource(data);
});
```

Using `.GetAwaiter().GetResult()` inside `MainLoop.Invoke` is safe because Terminal.Gui does not install a `SynchronizationContext`, so there is no deadlock risk from blocking.

## Background work pattern

Use `Task.Run` for long-running or async work. Use `MainLoop.Invoke` only to marshal results back to the UI thread:

```csharp
Task.Run(async () => {
    var result = await _service.SyncAsync();
    Application.MainLoop.Invoke(() => {
        UpdateUI(result);
    });
});
```

Never access UI controls from inside `Task.Run` without wrapping in `MainLoop.Invoke`.

## Timer cleanup

Every call to `Application.MainLoop.AddTimeout` must have a corresponding `RemoveTimeout` path. If a method like `ShowSpinner()` can be called multiple times, it must remove/cancel the previous timer before creating a new one:

```csharp
private object? _spinnerToken;

void ShowSpinner() {
    if (_spinnerToken != null)
        Application.MainLoop.RemoveTimeout(_spinnerToken);
    _spinnerToken = Application.MainLoop.AddTimeout(TimeSpan.FromMilliseconds(200), _ => { ... });
}
```

## Don't mix UI updates and async DB calls in one method

Keep UI-thread code and async/background code in separate methods or blocks. A single method that does both is a threading bug waiting to happen.
