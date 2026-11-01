---
name: quickdock-multimonitor
description: Multi-monitor enumeration, coordinate normalization, mixed DPI, and per-display state management.
---

# QuickDock Multi-Monitor Guidelines

## Physical Monitor Enumeration
- Enumerate displays dynamically via `EnumDisplayMonitors` Win32 API.
- Do not assume positive coordinates: secondary monitors can have negative bounds (e.g. placed to the left or above primary display).
- Calculate per-monitor DPI scale via `GetDpiForMonitor` or `WM_DPICHANGED`.

## Independent Display State
- Exactly ONE notch HWND per connected display.
- Hovering/expanding display N must never expand, reposition, or clip display M.
- Handle `WM_DISPLAYCHANGE` by synchronizing `NotchWindowManager.SynchronizeDisplays()` to gracefully add/remove windows on display hot-plug.

