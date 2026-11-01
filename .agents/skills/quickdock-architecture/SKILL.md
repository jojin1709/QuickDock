---
name: quickdock-architecture
description: Architecture review patterns for QuickDock .NET 10 / WinUI 3 modular system.
---

# QuickDock Architecture Guidelines

## Service Ownership & Dependency Injection
- Singleton services are registered in `App.xaml.cs` via Microsoft.Extensions.DependencyInjection.
- `IAppLifecycleService` orchestrates startup and shutdown of all services and modules.
- Single responsibility: Exactly one service coordinates window instances (`NotchWindowManager`), positioning (`WindowPositioningService`), and monitor hardware (`MonitorService`).

## Lifecycle & Disposal Boundaries
- All background threads must link to `CancellationToken` chains.
- Event subscriptions between long-lived services and UI views must unsubscribe in `Closed` / `Dispose()`.
- Thread safety: Never mutate WinUI `DependencyObject` or `ObservableProperty` values from background threads. Use `DispatcherQueue.TryEnqueue()`.

