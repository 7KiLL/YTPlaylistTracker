# Command System

## Overview

Terminal.Gui defines 50+ commands across six categories. Three lifecycle commands are most important:

| Command | Trigger | Purpose |
|---------|---------|---------|
| **Activate** | Space, click | Toggle/prepare state (checkbox, list selection) |
| **Accept** | Enter, double-click | Confirm/submit (button press, dialog confirm) |
| **HotKey** | Alt+letter, shortcut key | Keyboard shortcut activation |

Each follows a two-phase pattern:
- **Pre-event** (`Activating`/`Accepting`) — cancellable via `args.Cancel = true`
- **Post-event** (`Activated`/`Accepted`) — fires after state changes

## Command Routing

Commands flow through the view hierarchy via `CommandRouting`:
- **Direct** — programmatic or view's own bindings
- **BubblingUp** — propagating up through SuperView chain
- **DispatchingDown** — parent delegating to SubView
- **Bridged** — crossing non-containment boundaries

## Bubbling

Parent views opt-in to hear SubView commands:

```csharp
myWindow.CommandsToBubbleUp = [Command.Activate, Command.Accept];
myWindow.Activated += (_, args) =>
{
    if (args.Value?.TryGetSource(out View? source) is true
        && source is CheckBox { Id: "darkMode" })
    {
        ApplyTheme(args.Value?.Value as CheckState?);
    }
};
```

## Dispatch Patterns

### Consume Dispatch (Composite owns SubView state)

```csharp
protected override bool ConsumeDispatch => true;
// SubView commands consumed, composite fires own events
```

### Relay Dispatch (Forward to target)

```csharp
protected override View? GetDispatchTarget(ICommandContext? ctx) => CommandView;
// Commands propagate through target
```

## Shortcut View

Displays command + help text + key binding:

```
┌─────────────────────────────────────────────────┐
│ [CommandView]    [HelpView]         [KeyView]   │
│  _Open File      Opens a file       Ctrl+O      │
└─────────────────────────────────────────────────┘
```

Key behaviors:
- Clicking anywhere activates it (single-control illusion)
- Space = Activate, Enter = Accept
- BubbleDown pattern ensures all interactions reach CommandView

## Declarative Binding

```csharp
// Views advertise capabilities
AddCommand(Command.Copy, () => Copy());

// Menus auto-resolve key + title + help from localized resources
MenuItem cutItem = new(editor, Command.Cut);
// Automatically: Key=Ctrl+X, Title="_Cut", HelpText="Cut to clipboard"
```

## Tracing

```csharp
using Terminal.Gui.Tracing;
Trace.EnabledCategories = TraceCategory.Command;
```

## See Also

- [Keyboard](keyboard.md) — key binding specifics
- [Events](events.md) — Cancellable Work Pattern
