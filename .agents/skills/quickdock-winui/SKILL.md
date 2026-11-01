---
name: quickdock-winui
description: WinUI 3 XAML patterns, DispatcherQueue rules, visual tree management, and fluid animations.
---

# QuickDock WinUI Guidelines

## UI Thread Discipline
- UI components, DependencyObjects, and XAML DataBindings must only be accessed on the main STA UI thread.
- Use `DispatcherQueue.TryEnqueue()` with null-checking for background thread marshaling.
- Handle `0x8001010E (RPC_E_WRONG_THREAD)` proactively by ensuring WinRT asynchronous operations run on the STA thread.

## Visual Styling & Animations
- Maintain Windows 11 fluent dark design: dark charcoal `#121214` to near-black `#09090B`.
- Use subtle border lines `#27272A` with restrained contrast.
- Idle notch height: 38–40px resting at top-center. Expanded dock width: ~860px.
- Transitions must be smooth: Collapsed -> Opening -> Expanded and vice-versa with cubic-bezier / spline easing.

