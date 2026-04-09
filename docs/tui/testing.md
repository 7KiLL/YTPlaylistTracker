# Testing Terminal.Gui Applications

## Input Injection

Deterministic testing without hardware — simulate keyboard and mouse:

```csharp
VirtualTimeProvider time = new();
using IApplication app = Application.Create(time);
app.Init(DriverRegistry.Names.ANSI);

// Keyboard
app.InjectKey(Key.A);
app.InjectKey(Key.Enter);

// Mouse (use helpers)
app.InjectSequence(InputInjectionExtensions.LeftButtonClick(new Point(10, 5)));
app.InjectSequence(InputInjectionExtensions.LeftButtonDoubleClick(new Point(10, 5)));
```

## Virtual Time

Instant time advancement — no real delays:

```csharp
VirtualTimeProvider time = new();
time.SetTime(new DateTime(2025, 1, 1, 12, 0, 0));

app.InjectMouse(press);
time.Advance(TimeSpan.FromMilliseconds(50));  // instant
app.InjectMouse(release);
```

## Two Injection Modes

| Mode | Use Case | Speed |
|------|----------|-------|
| **Direct** (default) | View behavior, commands, events | Fast |
| **Pipeline** | ANSI encoding/decoding validation | Slower |

## Test Pattern

```csharp
[Fact]
public void Button_Click_RaisesAccepting()
{
    VirtualTimeProvider time = new();
    using IApplication app = Application.Create(time);
    app.Init(DriverRegistry.Names.ANSI);

    Button button = new() { Text = "Click Me" };
    bool called = false;
    button.Accepting += (s, e) => called = true;

    app.InjectKey(Key.Enter);
    Assert.True(called);
}
```

## Driver Selection

| Driver | Platform | Use Case |
|--------|----------|----------|
| ANSI | Unix/macOS (default) | Pure ANSI, deterministic, test-friendly |
| Windows | Windows (default) | Native Win32 Console APIs |
| DotNet | Cross-platform | `System.Console`, max compatibility |

Always use ANSI driver for tests: `app.Init(DriverRegistry.Names.ANSI)`

## Logging

**Critical**: Do NOT use console loggers — they corrupt Terminal.Gui output.

```csharp
// File-based logging
Logging.Logger = new LoggerConfiguration()
    .WriteTo.File("app.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();
```

### Test Logging

```csharp
TestLogging.BindTo(testOutputHelper);        // Warning+ level
TestLogging.Verbose(testOutputHelper);       // All levels
```

### Event Tracing

```csharp
Trace.EnabledCategories = TraceCategory.Command | TraceCategory.Mouse;
// Categories: Command, Mouse, Keyboard, Navigation, Application, Configuration
```

Zero overhead in Release builds (`[Conditional("DEBUG")]`).

## Performance Monitoring

```bash
dotnet counters monitor --name MyApp Terminal.Gui
```

Metrics: drain input timing, callback duration, main loop iteration time, redraw frequency.

## Best Practices

| Practice | Rationale |
|----------|-----------|
| Always use virtual time | Deterministic, repeatable tests |
| Use ANSI driver | Cross-platform, testable |
| Default to Direct mode | Fastest for typical tests |
| Dispose applications | Prevent thread leaks |
| Never use `Thread.Sleep()` | Use `time.Advance()` instead |
