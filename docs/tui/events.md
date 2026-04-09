# Events & Cancellable Work Pattern

## Cancellable Work Pattern (CWP)

Terminal.Gui's standard pattern for all events. Five-step sequence:

1. Call virtual method (`OnXxx`)
2. Check cancellation flag
3. Raise event
4. Check cancellation again
5. Execute default behavior if not cancelled

## Four Event Recipes

### 1. Cancellable Property Changes

```csharp
// Uses CWPPropertyHelper for vetoing property changes
// Events: DataSourceChanging (cancellable) → DataSourceChanged
```

### 2. Cancellable Actions/Workflows

```csharp
// Manual CancelEventArgs or CWPWorkflowHelper
// Events: Activating (cancellable) → Activated
```

### 3. Simple Notifications

```csharp
// Standard EventHandler for announcement-only events
// No cancellation possible
```

### 4. MVVM Property Binding

```csharp
// INotifyPropertyChanged for data binding
// Views implementing IValue<T>: CheckBox, TextField, ListView, RadioGroup
```

## Event Naming Convention

| Phase | Event Name | Virtual Method |
|-------|-----------|---------------|
| Pre-action | `Activating` | `OnActivating()` |
| Post-action | `Activated` | `OnActivated()` |

Always `-ing` suffix for cancellable pre-events, `-ed` for post-events.

## Command Context

Events carry `ICommandContext` providing:
- Originating view via `WeakReference<View>`
- Binding pattern (keyboard/mouse/programmatic)
- Value chain via `IValue<T>` for ancestor inspection

## Best Practices

- Correct ordering: virtual method → event → default behavior
- Unsubscribe in `Dispose` to prevent memory leaks
- Use `Handled` rather than `Cancel` for input events
- Check both cancellation points before executing default logic

## See Also

- [Commands](commands.md) — command system
- [Keyboard](keyboard.md) / [Mouse](mouse.md) — input events
