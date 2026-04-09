# Mouse Handling

## Mouse Bindings (Recommended)

Declarative mapping from mouse events to commands:

```csharp
// Declare commands
AddCommand(Command.ScrollUp, () => ScrollVertical(-1));
AddCommand(Command.ScrollDown, () => ScrollVertical(1));

// Map mouse events
MouseBindings.Add(MouseFlags.WheelUp, Command.ScrollUp);
MouseBindings.Add(MouseFlags.WheelDown, Command.ScrollDown);
```

### Default Bindings (All Views)

```csharp
MouseBindings.Add(MouseFlags.LeftButtonPressed, Command.Activate);
MouseBindings.Add(MouseFlags.LeftButtonPressed | MouseFlags.Ctrl, Command.Context);
```

## Handling Clicks

Use `Activating` event with command context:

```csharp
view.Activating += (s, e) =>
{
    if (e.Context?.Binding is MouseBinding { MouseEvent: { } mouse })
    {
        Point pos = mouse.Position;  // viewport-relative
        HandleClick(pos);
        e.Handled = true;
    }
};
```

## Coordinate Systems

| Level | Origin | Property |
|-------|--------|----------|
| Screen | (0,0) = terminal top-left | `mouse.ScreenPosition` |
| Viewport | (0,0) = view's content top-left | `mouse.Position` |

Conversion: `view.ScreenToViewport()`, `view.ViewportToScreen()`

## Mouse State and Highlighting

```csharp
// Automatic hover/press feedback
view.MouseHighlightStates = MouseState.In | MouseState.Pressed;

// Hold-repeat (scroll buttons, spinners)
view.MouseHoldRepeat = MouseFlags.LeftButtonReleased;
view.Activating += (s, e) => { DoRepeatAction(); e.Handled = true; };
```

## Mouse Grab

Views with `MouseHighlightStates` or `MouseHoldRepeat` auto-grab on press. Manual grab for drag operations:

```csharp
protected override bool OnMouseEvent(Mouse mouse)
{
    if (mouse.Flags.HasFlag(MouseFlags.Button1Pressed))
    {
        App?.Mouse.GrabMouse(this);
        return true;
    }
    if (mouse.Flags.HasFlag(MouseFlags.Button1Released))
    {
        App?.Mouse.UngrabMouse();
        return true;
    }
    return false;
}
```

## Pipeline

```
ANSI Input → AnsiMouseParser → MouseInterpreter → ApplicationMouse → View → Commands
(1-based)    (0-based screen)   (click synthesis)   (routing)       (viewport) (Activate/Accept)
```

## See Also

- [Commands](commands.md) — how commands work with mouse events
- [Testing](testing.md) — mouse injection for tests
