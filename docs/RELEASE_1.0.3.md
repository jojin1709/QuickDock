# QuickDock 1.0.3 — Official Release Notes

**Release Date:** 2026-11-01  
**Target Platform:** Windows 11 / Windows 10 (Build 19045+)  
**Architecture:** x64  
**Developer:** JOJIN JOHN  
**Contact / LinkedIn:** [https://www.linkedin.com/in/jojin-john/](https://www.linkedin.com/in/jojin-john/)  
**License:** Proprietary (All Rights Reserved) — See [LICENSE](../LICENSE)  
**GitHub Repository:** [https://github.com/jojin1709/QuickDock](https://github.com/jojin1709/QuickDock)  

---

## 1. Executive Summary & Highlights

QuickDock 1.0.3 delivers critical input safety hardening, zero-lag cursor proximity tracking, and storyboard lifecycle stabilization. This release completely removes legacy global low-level mouse hooks (`WH_MOUSE_LL`), eliminating all mouse stutter and input hitching across the Windows desktop.

Engineered strictly on **C# 13, .NET 10, WinUI 3, and Windows App SDK 2.6.4**, QuickDock 1.0.3 achieves 100% stable runtime execution with zero warnings, zero errors, and all 240 automated test validations passing.

---

## 2. Key Enhancements & Fixes in v1.0.3

### 2.1. Critical Input Safety Hardening (Hook Removal)
- **Eliminated `WH_MOUSE_LL`:** Completely removed global low-level mouse hooks from `NotchWindowManager`. Windows input dispatch chain is never intercepted or delayed.
- **Microsecond Kernel Cursor Proximity:** Implemented a non-blocking 50ms `DispatcherTimer` querying Win32 `GetCursorPos`. Execution time measured at **~36 nanoseconds** per check.
- **State Debouncing:** Proximity state transitions (`Closed` $\rightarrow$ `Opening`, `Open` $\rightarrow$ `Closing`) are strictly debounced. Dispatcher queues are no longer flooded during fast cursor movements.

### 2.2. XAML Storyboard & Visual Lifecycle Stabilization
- **Crash Prevention (`0xc000027b`):** Guarded `MainWindow.Expand()` and `MainWindow.Collapse()` against unmeasured or `NaN` dimensions on `DockSurface`.
- **Focus Preservation:** Removed `BringToFront()` from hover peeks to ensure mouse movement never steals keyboard or active window focus from background applications. Explicit user click (`OnIdlePointerPressed`) activates the dock safely.

### 2.3. Win32 Subclass & Message Dispatch Isolation
- **Subclass ID Separation:** Reassigned `MainWindowSubclassId` to `2001`, resolving a collision with `GlobalHotkeyService` (`1001`) in `comctl32.dll`.
- **Filtered Setting Broadcasts:** Filtered out spurious `WM_SETTINGCHANGE` messages, ensuring multi-monitor reconciliation executes strictly on genuine `WM_DISPLAYCHANGE` topology events.

### 2.4. Developer Toolkit Repository Persistence
- **State Storage:** Injected `ISettingsManager` into `DeveloperViewModel`, persisting the selected Git repository path across application restarts.
- **Empty State UX:** Clear guidance displayed when no repository is selected.

---

## 3. Automated Test Verification

| Test Suite | Tests Run | Passed | Failed | Skipped | Status |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **QuickDock.Core.Tests** | 240 | 240 | 0 | 0 | **PASSED** |

---

## 4. Hardware Verification Summary

- **Multi-Monitor Physical Verification:** Fully verified on physical dual-display setup (`\\.\DISPLAY1`: 1536×864 @ 125%, `\\.\DISPLAY5`: 1920×1080 @ 100%).
- **Resource Footprint:** No unbounded resource growth observed in tested scenarios. CPU stabilizes at < 0.1% at idle.

---

## 5. Release Distribution Assets & Cryptographic Checksums

| Artifact | Architecture | File Size | Description | SHA-256 Checksum |
| :--- | :--- | :--- | :--- | :--- |
| **`QuickDock-Setup.exe`** | x64 | **~177 MB** | **Standalone Windows Installer (Recommended)** | `8120640F3ACA9F071F6B3A73C6C5C5ECA54FEF385922CCC109511DDEFEE19A69` |
| **`QuickDock-v1.0.3-win-x64.exe`** | x64 | **~177 MB** | Named Standalone Installer | `8120640F3ACA9F071F6B3A73C6C5C5ECA54FEF385922CCC109511DDEFEE19A69` |
| **`QuickDock-v1.0.3-win-x64.zip`** | x64 | **~109 MB** | Portable Application Package Archive | `9EB6FC2D0AFA5AA20466044A298DD9BB83D41BADE4E0E763A6DD21539A29E3F8` |


