# Borders & Adornments

## Adornment Layers

Every view has three adornments (outside → inside):

```
Margin → Border → Padding → [Content]
```

### Configuration

```csharp
view.BorderStyle = LineStyle.Rounded;        // sets style + thickness + title
view.Border.Thickness = new Thickness(1);    // uniform 1-cell border
view.Padding.Thickness = new Thickness(1);   // 1-cell inner spacing
view.Margin.Thickness = new Thickness(2);    // 2-cell outer spacing
```

`Thickness` takes `(left, top, right, bottom)` or a single uniform value.

## Border Properties

| Property | Type | Purpose |
|----------|------|---------|
| `BorderStyle` | `LineStyle` | Helper — sets LineStyle + Settings + Thickness |
| `Border.LineStyle` | `LineStyle` | Line characters (Single, Double, Rounded, etc.) |
| `Border.Settings` | `BorderSettings` | Flags: Title, Tab |
| `Border.Thickness` | `Thickness` | Rows/columns per side |

## Title Rendering

When `BorderSettings.Title` is set, title placement depends on thickness:

| Top Thickness | Result |
|--------------|--------|
| 1 | Title inline: `┌┤Title├──┐` |
| 2 | Title with cap: `╭─╮` above title row |
| 3 | Title in enclosed rectangle |

## Tab-Style Borders

For tabbed interfaces:

```csharp
view.Border.Settings = BorderSettings.Tab | BorderSettings.Title;
view.Border.TabSide = Side.Top;
view.Border.TabOffset = 0;
```

Focused tabs suppress the content-side edge for visual connection to content.

## Arrangement (Move & Resize)

Border provides the interaction surface:

| Flag | Mouse Behavior |
|------|---------------|
| `ViewArrangement.Movable` | Drag top border to move |
| `ViewArrangement.Resizable` | Drag any edge to resize |

Keyboard: `Ctrl+F5` enters arrange mode with visual indicators.

## ytpt Border Usage

- `MainWindow`: `LineStyle.Rounded`, `Thickness(1, 2, 1, 1)` — extra top row for title area
- `FrameView` panes: `LineStyle.Rounded` with `Theme.Frame` color scheme
- `Dialog`s: `LineStyle.Rounded`, shadows disabled globally

## See Also

- [Drawing](drawing.md) — LineCanvas for line intersection handling
- [Layout](layout.md) — arrangement system details
