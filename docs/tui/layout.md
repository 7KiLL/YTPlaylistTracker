# Layout System

## Pos and Dim

Declarative positioning — no hardcoded pixel values:

```csharp
var input = new TextField
{
    X = Pos.Right(label) + 1,   // relative to another view
    Y = Pos.Top(label),          // align vertically
    Width = Dim.Fill(),          // consume remaining space
    Height = Dim.Auto()          // size based on content
};
```

### Pos Types

| Type | Example | Description |
|------|---------|-------------|
| `Pos.Absolute(n)` | `X = 5` | Fixed position |
| `Pos.Center()` | `X = Pos.Center()` | Center in SuperView |
| `Pos.Percent(n)` | `X = Pos.Percent(50)` | Percentage-based |
| `Pos.Right(view)` | `X = Pos.Right(otherView)` | Right edge of another view |
| `Pos.Bottom(view)` | `Y = Pos.Bottom(otherView)` | Bottom edge of another view |
| `Pos.AnchorEnd(n)` | `X = Pos.AnchorEnd(10)` | Anchor to right/bottom |
| `Pos.Align()` | `X = Pos.Align(Alignment.Center)` | Aligned group |
| Arithmetic | `X = Pos.Right(v) + 2` | Combine with offsets |

### Dim Types

| Type | Example | Description |
|------|---------|-------------|
| `Dim.Absolute(n)` | `Width = 18` | Fixed size |
| `Dim.Fill()` | `Width = Dim.Fill()` | Fill remaining space |
| `Dim.Fill(margin)` | `Height = Dim.Fill(2)` | Fill minus margin |
| `Dim.Percent(n)` | `Width = Dim.Percent(50)` | Percentage |
| `Dim.Auto()` | `Width = Dim.Auto()` | Auto-size from content |
| `Dim.Func(fn)` | `Width = Dim.Func(_ => calc())` | Dynamic function |

## Dim.Auto Deep Dive

Three strategies via `DimAutoStyle`:
- **Text**: size from `View.Text` and `TextFormatter`
- **Content**: size from SubViews or `GetContentSize()`
- **Auto** (default): larger of Text and Content

```csharp
// Auto-size with constraints
Width = Dim.Auto(
    minimumContentDim: Dim.Absolute(10),
    maximumContentDim: Dim.Percent(100)
);
```

## Viewport and Content

- **Viewport**: visible rectangle (portal into content)
- **Content Area**: total scrollable content size
- `Viewport.Location` can be non-zero when scrolling

```csharp
view.SetContentSize(new Size(200, 150));
view.ScrollVertical(5);  // shifts Viewport.Location
```

## View Arrangement

Interactive move/resize via `ViewArrangement` flags:

```csharp
window.Arrangement = ViewArrangement.Movable | ViewArrangement.Resizable;
// Mouse: drag border to move/resize
// Keyboard: Ctrl+F5 → arrange mode → arrow keys
```

Splitter pattern:
```csharp
rightPane.Arrangement = ViewArrangement.LeftResizable;
rightPane.SuperViewRendersLineCanvas = true;
```

## ytpt Layout Pattern

3-pane layout using `FrameView` + `Pos.Right`:
```
┌──Profiles──┬──Playlists──┬──Videos──────────────┐
│ Profile 1  │ Playlist A  │ # Title Channel Added│
│ Profile 2  │ Playlist B  │ 1 Video1 Chan1  ...  │
└────────────┴─────────────┴──────────────────────-┘
```

Fixed widths for first two panes (18, 28), `Dim.Fill()` for video table.
