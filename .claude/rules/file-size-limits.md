# File Size Limits

## Hard Limits

- **300 lines max** per `.cs` file (excluding auto-generated code and migrations)
- If a file exceeds 300 lines, it must be split before adding more code

## How to Split

- **Partial classes** for UI views: split by responsibility (e.g., `MainWindow.KeyHandling.cs`, `MainWindow.Sync.cs`)
- **Extract services/helpers** for reusable logic that doesn't need UI state
- **One responsibility per file** — if a class handles sync, export, search, and settings, those are 4 files

## When Creating New Code

- Before adding a method to an existing file, check the line count
- If the file is near or over 300 lines, split first, then add
- Never justify "just one more method" in a large file

## Naming Conventions for Partial Classes

```
Views/
  MainWindow.cs              # Fields, constructor, UI setup
  MainWindow.KeyHandling.cs  # ProcessHotKey, ProcessKey
  MainWindow.Sync.cs         # OnSync, OnSyncAll, spinner
  MainWindow.Actions.cs      # Add, toggle, export, settings
```
