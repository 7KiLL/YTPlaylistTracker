# Configuration System

## ConfigurationManager

JSON-based configuration for themes, key bindings, and app settings. **Disabled by default** — requires explicit enablement.

```csharp
ConfigurationManager.Enable(ConfigLocations.All);
using IApplication app = Application.Create().Init();
```

## Configuration Locations (Precedence Low → High)

1. Hard-coded defaults (property initializers)
2. Library resources (`Terminal.Gui.Resources.config.json`)
3. App embedded resources (`Resources/config.json`)
4. Global home (`~/.tui/config.json`)
5. Global current (`./.tui/config.json`)
6. App home (`~/.tui/AppName.config.json`)
7. App current (`./.tui/AppName.config.json`)
8. Environment variable (`TUI_CONFIG`)
9. Runtime (`ConfigurationManager.RuntimeConfig`)

## Theme Management

```csharp
ThemeManager.Theme = "Dark";
ConfigurationManager.Apply();
```

Built-in themes: Default, Dark, Light, TurboPascal 5.

### Custom Themes (JSON)

```json
{
  "Theme": "MyCustomTheme",
  "Themes": [{
    "MyCustomTheme": {
      "Window.DefaultBorderStyle": "Double",
      "Schemes": [{
        "Base": {
          "Normal": { "Foreground": "Cyan", "Background": "Black", "Style": "Bold" }
        }
      }]
    }
  }]
}
```

## Key Binding Configuration

```json
{
  "Application.DefaultKeyBindings": {
    "Quit": { "All": ["Ctrl+Q"] },
    "Suspend": { "Linux": ["Ctrl+Z"], "Macos": ["Ctrl+Z"] }
  }
}
```

## View-Specific Settings

```json
{
  "Window.DefaultBorderStyle": "Single",
  "Dialog.DefaultShadow": "None",
  "Dialog.DefaultBorderStyle": "Heavy"
}
```

## Glyph Customization

```json
{
  "Glyphs.Checked": "\u2611",
  "Glyphs.UnChecked": "\u2610"
}
```

## ytpt Theme System

ytpt uses a custom `ThemePalette` system with 6 hardcoded TrueColor themes instead of `ConfigurationManager`. Could potentially expose themes via JSON config for user customization.
