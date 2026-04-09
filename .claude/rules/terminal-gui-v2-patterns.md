# Terminal.Gui v2 Patterns

Preferred patterns for Terminal.Gui v2. See `docs/tui/` for detailed reference.

## Command-Based Input (Preferred over raw KeyDown)

```csharp
// Declare what the view supports
AddCommand(Command.Accept, () => { Save(); return true; });
AddCommand(Command.New, () => { AddPlaylist(); return true; });

// Map keys declaratively
KeyBindings.Add(Key.Enter, Command.Accept);
KeyBindings.Add((Key)'a', Command.New);

// Mouse bindings follow the same pattern
MouseBindings.Add(MouseFlags.WheelUp, Command.ScrollUp);
```

Benefits: user-configurable via JSON, self-documenting, no manual text-field guards.

## Events: Accepting, Not Clicked

```csharp
// v2 pattern
button.Accepting += (s, e) => { DoAction(); };

// NOT: button.Clicked (removed in v2)
```

The `Activating`/`Accepting` pair follows the Cancellable Work Pattern:
- `Activating` — toggle/select state (cancellable)
- `Accepting` — confirm/submit (cancellable)

## Static Application API (Current)

Our codebase uses the static API which is fine for Terminal.Gui 2.0.0:

```csharp
Application.Invoke(() => UpdateUI());           // marshal to UI thread
Application.AddTimeout(span, () => true);       // timer (return true = repeat)
Application.RemoveTimeout(token);               // cancel timer
Application.RequestStop();                       // close modal dialog
```

When upgrading to newer Terminal.Gui, prefer instance-based `View.App?.Invoke()`.

## Layout: Pos and Dim

Always use declarative positioning:

```csharp
view.X = Pos.Right(otherView) + 1;
view.Width = Dim.Fill();       // fill remaining space
view.Height = Dim.Fill(2);     // fill minus 2 for hint bar
```

Avoid hardcoded absolute positions when relative positioning works.

## Color: Scheme over ColorScheme

```csharp
// Current (v1 pattern, works in 2.0.0)
view.ColorScheme = Colors.ColorSchemes["Base"];

// v2 pattern (when available)
view.Scheme = SchemeManager.GetScheme(Schemes.Base);
SetAttributeForRole(VisualRole.Normal);
```

## Dialog Pattern

```csharp
var dialog = new Dialog { Title = "Confirm", Width = 40, Height = 8 };
var okBtn = new Button { Text = "OK", IsDefault = true };
okBtn.Accepting += (s, e) => Application.RequestStop();
dialog.AddButton(okBtn);
Application.Run(dialog);  // blocks until RequestStop
```

## Focus and Navigation

- Set `CanFocus = true` explicitly (defaults to false in v2)
- Use `TabStop = TabBehavior.TabStop` for tab navigation
- Use `view.SetFocus()` to programmatically focus
- Tab/Shift+Tab cycles TabStops; F6/Shift+F6 cycles TabGroups

## Timer Safety

Every `AddTimeout` must have a `RemoveTimeout`. Clean up before creating new timers:

```csharp
private object? _timer;

void StartTimer() {
    if (_timer != null) Application.RemoveTimeout(_timer);
    _timer = Application.AddTimeout(TimeSpan.FromMilliseconds(200), () => {
        UpdateFrame();
        return true;  // true = continue
    });
}
```

## Testing TUI Code

Use input injection for deterministic tests:

```csharp
VirtualTimeProvider time = new();
using IApplication app = Application.Create(time);
app.Init(DriverRegistry.Names.ANSI);

app.InjectKey(Key.Enter);
app.InjectSequence(InputInjectionExtensions.LeftButtonClick(new Point(5, 3)));
time.Advance(TimeSpan.FromMilliseconds(500));
```

Never use `Thread.Sleep()` in TUI tests — use `time.Advance()`.
