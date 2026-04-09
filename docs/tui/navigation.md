# Navigation & Focus

## Focusability Requirements

For keyboard navigation, a view must satisfy all four:
1. `Visible = true`
2. `Enabled = true`
3. `CanFocus = true` (defaults to `false` in v2)
4. `TabStop != TabBehavior.NoStop`

Mouse focus only requires the first three.

## TabBehavior

| Value | Behavior |
|-------|----------|
| `NoStop` | No keyboard focus, but mouse/code can focus |
| `TabStop` | Focusable; Tab/Shift+Tab cycle through peers |
| `TabGroup` | Container; F6/Shift+F6 cycle across TabGroups |
| `null` | Initialization state — auto-resolved on `Add()` |

## Navigation Keys

| Key | Action |
|-----|--------|
| Tab / Shift+Tab | Next/previous TabStop |
| F6 / Shift+F6 | Next/previous TabGroup |
| Arrow keys | Same as Tab navigation by default |
| Alt+letter | HotKey — direct jump to view |
| Enter / Space | Activate focused view |

## Focus Management

```csharp
view.CanFocus = true;
view.TabStop = TabBehavior.TabStop;
view.SetFocus();  // request focus, returns bool

// Monitor focus changes
view.HasFocusChanging += (s, e) => { e.Cancel = true; }; // prevent
view.HasFocusChanged += (s, e) => { UpdateVisual(); };    // react
```

## Application Navigation

`Application.Navigation` provides centralized navigation:
- `GetFocused()` — returns most-focused view
- `AdvanceFocus()` — programmatic focus advancement
- `FocusedChanged` / `FocusedChanging` events

## HotKeys

Activate views regardless of focus state. Defined with `_` prefix in text:

```csharp
var btn = new Button { Text = "_Save" }; // Alt+S activates
```

Multiple views can share the same HotKey.

## Visual Indicators

- Focused views use `ColorScheme.Focus` / `Scheme.Focus` attributes
- HotKey characters appear underlined
- Terminal cursor shown on deepest focused view

## Accessibility

- Keyboard alternatives for all mouse actions
- Logical tab ordering
- Clear focus visibility
- Color-independent indicators

## ytpt Navigation

Uses vim-style `h/j/k/l` plus arrows for 3-pane navigation, intercepted via `Application.KeyDown` before views process keys.
