# Scrolling

## Built-in Scrolling

Every `View` supports scrolling via `Viewport` / `ContentSize`:

```csharp
// 1. Set content larger than viewport
view.SetContentSize(new Size(200, 150));

// 2. Scroll
view.ScrollVertical(5);
view.ScrollHorizontal(3);

// 3. Enable automatic scrollbars
view.ViewportSettings |= ViewportSettingsFlags.HasVerticalScrollBar;
view.ViewportSettings |= ViewportSettingsFlags.HasHorizontalScrollBar;
```

No need for a `ScrollView` wrapper — scrolling is built into every view.

## ScrollBar Visibility Modes

| Mode | Behavior |
|------|----------|
| `Manual` | Always hidden unless shown programmatically |
| `Auto` (default with flag) | Show only when content exceeds viewport |
| `Always` | Always visible |
| `None` | Never visible |

## ViewportSettings Flags

| Category | Flags |
|----------|-------|
| Negative location | `AllowNegativeX`, `AllowNegativeY` |
| Overflow | `AllowXGreaterThanContentWidth`, `AllowYGreaterThanContentHeight` |
| Blank space | `AllowXPlusWidthGreaterThanContentWidth` |
| Drawing | `ClipContentOnly`, `ClearContentOnly`, `Transparent` |
| ScrollBars | `HasVerticalScrollBar`, `HasHorizontalScrollBar` |

## Key Bindings for Scrolling

Add bindings manually — views don't include scroll bindings by default:

```csharp
AddCommand(Command.ScrollUp, () => ScrollVertical(-1));
AddCommand(Command.ScrollDown, () => ScrollVertical(1));
KeyBindings.Add(Key.CursorUp, Command.ScrollUp);
KeyBindings.Add(Key.CursorDown, Command.ScrollDown);
MouseBindings.Add(MouseFlags.WheelUp, Command.ScrollUp);
MouseBindings.Add(MouseFlags.WheelDown, Command.ScrollDown);
```
