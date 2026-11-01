---
name: quickdock-win32
description: Win32 P/Invoke, window subclassing, message hooking, and native interop rules for QuickDock.
---

# QuickDock Win32 Guidelines

## Window Subclassing & Messages
- Use `SetWindowSubclass` / `RemoveWindowSubclass` (comctl32.dll) rather than `SetWindowLongPtr(GWLP_WNDPROC)` to prevent hooking collisions.
- The `SUBCLASSPROC` delegate must be retained in a private instance field to prevent garbage collection while registered.
- Handle `WM_CLIPBOARDUPDATE (0x031D)` for clipboard notifications via `AddClipboardFormatListener`.

## Window Styles & ToolWindow
- QuickDock windows must set `WS_EX_TOOLWINDOW` and strip `WS_EX_APPWINDOW` to remain completely invisible from Alt+Tab and taskbar.
- Borderless and chrome removal is achieved via `OverlappedPresenter.SetBorderAndTitleBar(false, false)`.

