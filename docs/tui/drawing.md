# Drawing & Colors

## Drawing Lifecycle

Each MainLoop iteration:
1. **Layout** — measure and position views
2. **Draw** — update driver's back buffer
3. **Write** — flush changed cells to terminal
4. **Cursor** — position terminal cursor

Drawing is deferred — `View.Draw()` updates the buffer, actual output happens in `Refresh()`.

## Coordinate System

All draw APIs use **viewport-relative** coordinates. `(0, 0)` = top-left visible cell.

```csharp
protected override bool OnDrawingContent()
{
    Move(0, 0);                             // position in viewport
    SetAttributeForRole(VisualRole.Normal);  // set colors
    AddStr("Hello");                         // output text
    return true;
}
```

## 24-bit TrueColor

```csharp
Color custom = new(0xFF, 0x99, 0x00);      // RGB
Color named  = new("#cba6f7");              // hex string
Color std    = Color.Yellow;                // named
bool dark    = color.IsDarkColor();         // HSL lightness < 50%
```

## Scheme and VisualRole (v2)

`Scheme` maps semantic roles to `Attribute` (foreground + background + style):

| Role | Purpose |
|------|---------|
| `Normal` | Default state |
| `Focus` | Focused state |
| `HotNormal` | HotKey character, unfocused |
| `HotFocus` | HotKey character, focused |
| `Disabled` | Disabled state |

```csharp
// v2 pattern (Scheme)
view.Scheme = SchemeManager.GetScheme(Schemes.Dialog);
SetAttributeForRole(VisualRole.Normal);

// v1 pattern (ColorScheme) — still works in 2.0.0
view.ColorScheme = Colors.ColorSchemes["Base"];
```

### Scheme Inheritance

Views inherit `Scheme` from parent. Override with explicit assignment or `GettingScheme` event.

## Text Styles

Combinable via `TextStyle`:
`None`, `Bold`, `Faint`, `Italic`, `Underline`, `Blink`, `Reverse`, `Strikethrough`

```csharp
new Attribute(Color.White, Color.Black, TextStyle.Bold | TextStyle.Underline)
```

## LineCanvas

Automatic Unicode box-drawing intersection resolution:

```csharp
LineCanvas lc = new();
lc.AddLine(new Point(0, 0), 10, Orientation.Horizontal, LineStyle.Single);
lc.AddLine(new Point(4, 0), 5, Orientation.Vertical, LineStyle.Single);
// At (4,0): auto-resolves to ┼
```

### Line Styles

`Single` (─), `Double` (═), `Heavy` (━), `Rounded` (╭), `Dashed` (╌), `Dotted` (┄)

### Canvas Merging

Each View has its own `LineCanvas`. `SuperViewRendersLineCanvas = true` merges SubView lines into parent for proper intersections.

## Glyphs

`Glyphs` class defines common characters (checkboxes, arrows, borders). Customizable via `ConfigurationManager`.

## ytpt Color Pattern

Uses `ThemePalette` records with TrueColor hex values → builds `ColorScheme` objects in `Theme.Apply()`. Six themes: Catppuccin, Dracula, Gruvbox Dark, Nord, High Contrast Dark/Light.

## See Also

- [Borders](borders.md) — adornment styling
- [Configuration](configuration.md) — theme management
