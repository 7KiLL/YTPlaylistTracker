# Application Lifecycle

## Instance-Based Model (v2)

v2 moves from static singletons to `IApplication` instances:

```csharp
// Recommended v2 pattern
using IApplication app = Application.Create().Init();
app.Run<MainWindow>();
// app.Dispose() called automatically via using
```

Key concepts:
- `Application.Create()` returns `IApplication`
- `View.App` gives any view access to its owning application context
- `IApplication.SessionStack` tracks running sessions as a stack
- `TopRunnable` references the currently active modal runnable

## Current ytpt Pattern

ytpt still uses the static pattern (`Application.Init()` / `Application.Run()` / `Application.Shutdown()`). The instance-based pattern improves testability and enables multiple application contexts.

## IRunnable Architecture

Type-safe modal views with results:

```csharp
public class FileDialog : Runnable<string?>
{
    public FileDialog()
    {
        Button ok = new() { Text = "OK", IsDefault = true };
        ok.Accepting += (s, e) => {
            Result = _pathField.Text;
            Application.RequestStop();
        };
    }
}

// Fluent usage
app.Run<FileDialog>();
string? path = app.GetResult<string>();
```

## Disposal Rules

"Whoever creates it, owns it":
- `Run<T>()` — framework creates → framework disposes automatically
- `Run(IRunnable)` — caller creates → caller must dispose
- `view.Add(subView)` — SuperView owns subView unless removed

## Application Events

- Input thread runs at ~50 polls/second until disposed
- Always dispose applications in tests to prevent thread leaks
- `Shutdown()` is obsolete — use `Dispose()` instead

## See Also

- [Multitasking](multitasking.md) — modal vs non-modal execution
- [Testing](testing.md) — instance-based testing patterns
