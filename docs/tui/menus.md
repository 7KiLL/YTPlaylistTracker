# Menus & Popovers

## Menu Class Hierarchy

```
Shortcut → MenuItem → MenuBarItem     (items)
Bar      → Menu     → MenuBar         (containers)
PopoverMenu                            (cascading popover)
```

## MenuBar

Horizontal menu with dropdown submenus:

```csharp
var menuBar = new MenuBar
{
    Menus = [
        new MenuBarItem("_File", [
            new MenuItem("_Open", "", () => Open()),
            new MenuItem("_Save", "", () => Save()),
            null,  // separator
            new MenuItem("_Quit", "", () => Application.RequestStop()),
        ])
    ]
};
```

**Keyboard**: F10 activates, arrows navigate, Enter accepts, Esc closes.

## PopoverMenu (Context Menus)

```csharp
var contextMenu = new PopoverMenu([
    new MenuItem("_Edit", "", () => Edit()),
    new MenuItem("_Delete", "", () => Delete()),
]);
Application.Popover?.Register(contextMenu);

// Show on right-click
view.MouseEvent += (s, e) => {
    if (e.Flags.HasFlag(MouseFlags.RightButtonPressed)) {
        contextMenu.MakeVisible();
    }
};
```

### Popover Requirements

A popover must: implement `IPopoverView`, set `CanFocus = true`, include `ViewportSettings.Transparent` and `TransparentMouse`, bind quit key.

### Lifecycle

- Register before showing (`Application.Popover.Register()`)
- Single-active constraint: showing one hides the previous
- Clicks outside SubViews auto-dismiss (transparent overlay technique)

## Shortcut View

Foundation for menu items — shows command + help + key binding:

```csharp
var shortcut = new Shortcut
{
    Key = Key.F6,
    HelpText = "Toggle tracking",
    CommandView = new CheckBox { Text = "Track" },
    Action = () => ToggleTrack()
};
```

Clicking anywhere on a Shortcut activates it (single-control illusion via BubbleDown pattern).

## ytpt Menu Usage

Currently uses `ContextMenu` for profile actions. Could migrate to `PopoverMenu` for richer context menus.
