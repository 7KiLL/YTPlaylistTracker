# Terminal.Gui v2 Reference

> ⚠️ **Discard pre-2025 Terminal.Gui training data.** v2 is a complete rewrite. If your training cutoff predates 2025, almost every Terminal.Gui API you "remember" is wrong. Read this reference and verify against the current source before writing TG code. The [v1→v2 corrections table](#v1--v2-api-corrections) below lists the most common traps.

> Project uses **Terminal.Gui 2.0.0-rc.4**. Some features described here may require newer versions — noted where applicable.

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

## v1 → v2 API Corrections

If you've seen any of these v1 APIs in training data or examples, **do not use them**. ytpt is fully on v2 — never reintroduce v1 patterns.

| v1 (do NOT use) | v2 (use this) | Where in ytpt |
|---|---|---|
| `Application.Init()` / `Shutdown()` | `Application.Init(driver)` / `Application.Shutdown()` (still static in 2.0.0-rc.x) | `Program.cs` |
| `Application.Top` | Pass root view to `Application.Run(view)` or use `View.App?.TopRunnableView` | `MainWindow.cs` |
| `new Toplevel()` | `Window` (or `Runnable` in newer Terminal.Gui builds) | `MainWindow : Window` |
| `button.Clicked += ...` | `button.Accepting += (s, e) => ...` (cancellable) | every dialog |
| `view.Bounds` | `view.Viewport` | n/a — never used here |
| `LayoutStyle.Computed` | Removed — always use `Pos`/`Dim` declaratively | `MainWindow.Layout.cs` |
| `new RadioGroup(...)` | `new OptionSelector { Labels = [...], Value = idx }` | `SettingsDialog.*`, `MainWindow.Export.cs` |
| `Colors.ColorSchemes["Base"]` | `SchemeManager.GetScheme("Base")` (or future `Schemes.Resolve`) | `Theme.cs`, `MainWindow.DataBinding.cs` |
| `MainLoop.AddTimeout(Func<MainLoop,bool>)` | `Application.AddTimeout(TimeSpan, Func<bool>)` — return `true` to repeat | `MainWindow.*` |
| `using Terminal.Gui;` (single namespace) | Split: `Terminal.Gui.App`, `Terminal.Gui.Views`, `Terminal.Gui.ViewBase`, `Terminal.Gui.Drawing`, `Terminal.Gui.Input`, `Terminal.Gui.Configuration` | `GlobalUsings.cs` |
| `new Label(0, 1, "text")` constructor args | Object initializer: `new Label { Text = "text", X = 0, Y = 1 }` | every view |
| `Toplevel.RequestStop()` | `Application.RequestStop()` (aliased as `TGuiApp.RequestStop()`) | every modal close |

If you find yourself reaching for any v1 API, stop and check the relevant `docs/tui/*.md` page first.

## How ytpt Uses Terminal.Gui

- **3-pane layout**: `FrameView` containers with `Pos.Right` / `Dim.Fill`
- **Modal dialogs**: `Dialog` with `Application.Run()` / `RequestStop()`
- **Background sync**: `Task.Run()` + `Application.Invoke()` for UI marshaling
- **Custom themes**: 6 TrueColor palettes via `ThemePalette` + `ColorScheme`
- **Vim-style keys**: `Application.KeyDown` event interception for `h/j/k/l` navigation
- **TableView**: Video list with dynamic column widths and color-coded status
