# Views

## View Lifecycle

1. **Constructor** — creates view with defaults
2. **`BeginInit()` / `EndInit()`** — bracket initialization
3. **`Initialized` event** — fires when setup completes
4. **Rendering**: Layout → Draw → Write → Cursor (each MainLoop iteration)
5. **`Dispose()`** — cleans up resources; SubViews auto-dispose with SuperView

## Compositional Layers

Every view has nested rectangular layers (outside → inside):

```
Frame → Margin → Border → Padding → Viewport → Content Area
```

- **Frame**: outermost boundary, position relative to SuperView
- **Margin**: transparent spacing outside border
- **Border**: visual frame with title, configurable `LineStyle`
- **Padding**: spacing between border and content
- **Viewport**: visible "window" into scrollable content
- **Content Area**: total drawable region

## Key Built-in Views Used in ytpt

| View | Usage in ytpt |
|------|--------------|
| `Window` | MainWindow base class |
| `Dialog` | All modal dialogs (Add, Settings, Help, Detail, etc.) |
| `FrameView` | 3-pane layout containers (Profiles, Playlists, Videos) |
| `ListView` | Profile list, playlist list |
| `TableView` | Video table with column styles |
| `Label` | Headers, status info, hint bar |
| `TextField` | Text inputs (URL, search, name) |
| `Button` | Dialog actions |
| `CheckBox` | Settings toggles |
| `RadioGroup` | Theme selector, export format |
| `TextView` | Read-only description display |
| `MessageBox` | Confirmations and info |
| `ContextMenu` | Profile right-click actions |

## Notable v2 Views Not Yet Used

| View | Potential Use |
|------|-------------|
| `SpinnerView` | Replace custom spinner timer implementation |
| `PopoverMenu` | Context menus, right-click actions |
| `NumericUpDown<T>` | Numeric settings |
| `Tabs` | Tab-based navigation alternative |
| `DropDownList` | Compact selection (theme, format) |
| `Link` | Clickable hyperlinks in detail views |
| `ColorPicker` | Theme customization |
| `Prompt<T>` | Quick single-input dialogs with type-safe results |

## Custom Drawing

```csharp
protected override bool OnDrawingContent()
{
    Move(0, 0);  // viewport-relative, not SuperView-relative
    SetAttributeForRole(VisualRole.Normal);
    AddStr("Custom content");
    return true;
}
```

## See Also

- [Layout](layout.md) — Pos/Dim positioning
- [Drawing](drawing.md) — colors and rendering
- [Borders](borders.md) — adornment system
