# Keyboard Handling

## Core Tenets (Priority Order)

1. **User Control** — defaults are configurable by the user
2. **Editor-Like Behavior** — follow GUI conventions (VS Code, Vim) not CLI idioms
3. **Platform Consistency** — match OS expectations (Ctrl+Backspace on Windows, Ctrl+W on Linux)
4. **Hot Key Functionality** — visible HotKeys must be activatable

## Key Class

Platform-independent keyboard abstraction:

```csharp
// Key comparisons
if (key == Key.Enter) { }
if (key == Key.C.WithCtrl) { }

// Modifier checks
key.Shift  // true if Shift held
key.Ctrl   // true if Ctrl held
key.Alt    // true if Alt held

// Handled flag
key.Handled = true;  // stop further processing
```

## Key Bindings (Recommended Approach)

Command-based system — views declare commands, then map keys:

```csharp
// 1. Declare what the view can do
view.AddCommand(Command.Accept, () => { HandleAccept(); return true; });

// 2. Map keys to commands
view.KeyBindings.Add(Key.Enter, Command.Accept);
view.KeyBindings.Add(Key.Space, Command.Activate);
```

### Three Binding Layers

1. **`Application.DefaultKeyBindings`** — global (Quit, Suspend, navigation)
2. **`View.DefaultKeyBindings`** — base view (clipboard, editing)
3. **Per-view** — view-specific (TextField Emacs keys, etc.)

### Platform-Aware Bindings

```csharp
// Different keys per platform
Application.DefaultKeyBindings[Command.Quit] = Bind.All(Key.Esc);
```

## HotKeys vs Shortcuts

- **HotKey**: Alt+character for visible items (e.g., `_OK` → Alt+O). Defined with `_` prefix.
- **Shortcut**: opinionated view showing command + help + key binding (used in menus/status bars)

## Key Events (Direct Handling)

For cases where command bindings aren't sufficient:

```csharp
// Application-level (fires BEFORE views)
Application.KeyDown += (sender, key) => {
    if (key == (Key)'j') {
        // handle vim down
        key.Handled = true;
    }
};
```

### Event Flow

1. **Before**: check enabled → route to focused subviews
2. **During**: `OnKeyDown()` → check key bindings
3. **After**: `OnKeyDownNotHandled()` if unprocessed

## Kitty Keyboard Protocol

Enhanced keyboard support in modern terminals (Windows Terminal, kitty, WezTerm, Ghostty):
- Disambiguated escape codes
- Key release events (`KeyUp`)
- Repeat detection
- Standalone modifier events

## ytpt Keyboard Pattern

Uses `Application.KeyDown` for vim-style navigation (h/j/k/l, arrow keys, Shift+arrows for fast scroll). Guards against intercepting keys during text input:

```csharp
// Skip when text field has focus
if (Application.Top?.MostFocused is TextField or TextView)
    return;
```

### Improvement Opportunity

Current manual `Application.KeyDown` handling could use `AddCommand()` + `KeyBindings` for declarative, user-configurable bindings.

## See Also

- [Commands](commands.md) — command system details
- [Navigation](navigation.md) — focus and tab navigation
