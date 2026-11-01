# QuickDock Phase 12: Packaging, Multi-Monitor Lifecycle & Release Hardening

## Overview
QuickDock 1.0.0 is packaged as a high-performance native standalone Windows 11 utility with an embedded single-file installer and clean uninstaller lifecycle.

---

## 1. Release Artifacts & Checksums

| File | Type | Architecture | SHA-256 Digest |
| :--- | :--- | :--- | :--- |
| `QuickDock-Setup.exe` | Standalone Single-File Setup | win-x64 | `714BEB15FC020A2A23925C90A5591D78BF2D9BBC186C142BC9C924F80CACC9C8` |
| `QuickDock-1.0.0-win-x64.exe` | Standalone Installer | win-x64 | `714BEB15FC020A2A23925C90A5591D78BF2D9BBC186C142BC9C924F80CACC9C8` |
| `QuickDock-v1.0.0-win-x64.zip` | Standalone Application Archive | win-x64 | `5576BE2E8A4778A9C36B1D4528BD74AA0FBC67DA4F207E5396BCFA0A984D4B76` |

---

## 2. Windows Settings & Installed Apps Uninstallation Mechanism

### Root Cause of Previous Bug
Previously, Windows 11 Installed Apps invoked `UninstallString: "QuickDock-Setup.exe" /uninstall`. Because `QuickDock.Setup` had no command-line argument handling and launched `StartupUri="MainWindow.xaml"`, executing the uninstaller unexpectedly launched the first-time onboarding wizard.

### Corrective Resolution
1. **Explicit Startup Interception:**
   Removed `StartupUri` from `App.xaml`. `App.xaml.cs` explicitly inspects `e.Args` for `/uninstall`, `-uninstall`, and `--uninstall`.
2. **Decoupled Worker Architecture:**
   Because `QuickDock-Setup.exe` resides inside `%LOCALAPPDATA%\Programs\QuickDock`, it spawns `%TEMP%\QuickDock-Uninstall.exe /uninstall /worker` and exits immediately so the installation directory can be deleted without file locks.
3. **Thorough System Cleanup:**
   - Terminates all active `QuickDock.App`, `QuickDock`, and `QuickDock-Setup` processes.
   - Removes Desktop shortcut: `%USERPROFILE%\Desktop\QuickDock.lnk`.
   - Removes Start Menu shortcut: `%APPDATA%\Microsoft\Windows\Start Menu\Programs\QuickDock.lnk`.
   - Removes Windows Run key: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\QuickDock`.
   - Removes Windows Installed Apps registration: `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\QuickDock`.
   - Deletes `%LOCALAPPDATA%\Programs\QuickDock` recursively.
   - Cleans up worker process without launching any setup or onboarding UI.

---

## 3. Multi-Monitor & Top-Center Notch Architecture

1. **Independent Per-Monitor Windows:**
   - `NotchWindowManager` instantiates and manages an independent `MainWindow` for each connected physical monitor.
   - Each window maintains its own `TargetMonitor`, DPI scale, work area coordinates, and `IsExpanded` state.
   - Removed legacy single-window monitor following timers that previously caused window leaping, clipping, and duplicate windows.
2. **Sub-millisecond Global Hit-Zone Detection:**
   - Centralized low-level mouse hook (`WH_MOUSE_LL`) monitors cursor position with sub-microsecond coordinate checking.
   - Configurable top hit-zone covers the top 12 physical pixels and notch width with horizontal tolerance.
   - Deterministic debounce timers:
     - Open: 80–120 ms
     - Close: 180 ms
3. **Seamless Panel Navigation:**
   - Moving from notch capsule into the expanded dashboard stays inside the composite interaction zone (`TopHitZone + ExpandedBounds`), eliminating accidental collapse.
4. **DPI & Multi-Monitor Geometry:**
   - Atomic window positioning with scaled rounded corners (`scaleFactor`) eliminates visual clipping across 100%, 125%, 150%, 175%, and 200% scaling factors.
   - Negative coordinate support for displays placed left of or above primary display.
