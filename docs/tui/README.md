# Terminal.Gui v2 Reference

> Project uses **Terminal.Gui 2.0.0**. Some features described here may require newer versions — noted where applicable.

Quick reference for the Terminal.Gui v2 APIs and patterns used in ytpt.

## Topics

| Topic | What it covers |
|-------|---------------|
| [Application](application.md) | Lifecycle, `IApplication`, disposal, `IRunnable` |
| [Views](views.md) | Built-in view catalog, View lifecycle, composition |
| [Layout](layout.md) | `Pos`/`Dim`, `Dim.Auto`, arrangement, responsive design |
| [Drawing & Colors](drawing.md) | Drawing lifecycle, 24-bit color, `Scheme`/`VisualRole`, `LineCanvas` |
| [Keyboard](keyboard.md) | `Key` class, key bindings, commands, HotKeys |
| [Mouse](mouse.md) | Mouse bindings, events, grab, coordinate systems |
| [Commands](commands.md) | Command enum, routing, bubbling, dispatch patterns |
| [Events](events.md) | Cancellable Work Pattern, event recipes |
| [Navigation](navigation.md) | Focus, TabStop/TabGroup, accessibility |
| [Menus](menus.md) | MenuBar, PopoverMenu, context menus, Shortcut view |
| [Scrolling](scrolling.md) | Viewport, ScrollBar, ViewportSettings |
| [Multitasking](multitasking.md) | Threading, async, timers, `App.Invoke` |
| [Configuration](configuration.md) | ConfigurationManager, themes, JSON config |
| [Testing](testing.md) | Input injection, virtual time, drivers, logging |

## How ytpt Uses Terminal.Gui

- **3-pane layout**: `FrameView` containers with `Pos.Right` / `Dim.Fill`
- **Modal dialogs**: `Dialog` with `Application.Run()` / `RequestStop()`
- **Background sync**: `Task.Run()` + `Application.Invoke()` for UI marshaling
- **Custom themes**: 6 TrueColor palettes via `ThemePalette` + `ColorScheme`
- **Vim-style keys**: `Application.KeyDown` event interception for `h/j/k/l` navigation
- **TableView**: Video list with dynamic column widths and color-coded status
