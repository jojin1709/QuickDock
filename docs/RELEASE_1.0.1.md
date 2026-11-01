# QuickDock 1.0.1 — Official Release Notes

**Release Date:** 2026-11-01  
**Target Platform:** Windows 11 / Windows 10 (Build 19045+)  
**Architecture:** x64  
**Developer:** JOJIN JOHN  
**Contact / LinkedIn:** [https://www.linkedin.com/in/jojin-john/](https://www.linkedin.com/in/jojin-john/)  
**License:** Proprietary (All Rights Reserved) — See [LICENSE](../LICENSE)  
**GitHub Repository:** [https://github.com/jojin1709/QuickDock](https://github.com/jojin1709/QuickDock)  

---

## 1. Executive Summary & Highlights

QuickDock 1.0.1 is a targeted release hardening and runtime stability update following rigorous acceptance gate testing across physical multi-monitor hardware, mixed-DPI environments, and high-frequency lifecycle stress cycles.

Engineered strictly on **C# 13, .NET 10, WinUI 3, and Windows App SDK 2.6.4**, QuickDock 1.0.1 refines multi-monitor hit testing, eliminates clipboard COM thread marshalling errors, hardens lifecycle memory bounds, and packages the modern standalone single-file installer.

---

## 2. Key Enhancements & Fixes in v1.0.1

### 2.1. Multi-Monitor Hardware Isolation
- **Isolated Per-Monitor Top-Edge Hit Testing:** Each connected physical monitor operates an independent notch instance (`NotchWindowManager`) with its own top-edge hover detection zone.
- **Zero Cross-Monitor Crosstalk:** Moving cursor between displays no longer causes notch duplication, clipping, or unintended window repositioning. Expanding one display's dock leaves other displays cleanly untouched.
- **Mixed DPI Awareness:** Dynamic coordinate normalization across mixed DPI displays (e.g. 125% laptop display + 100% desktop monitor).

### 2.2. Thread-Safe Clipboard Management
- **Win32 & WinRT Marshaling Defense:** Resolved `RPC_E_WRONG_THREAD (0x8001010E)` exception when background clipboard events notify the WinUI 3 visual tree.
- **STA Thread Dispatching:** All clipboard state mutations and notifications are guaranteed to execute on the main UI thread via `DispatcherQueue.TryEnqueue()`.
- **Deduplication:** High-frequency repeated identical clipboard contents are cleanly deduplicated with zero UI stutter.

### 2.3. Resource & Lifecycle Stability
- **No Unbounded Resource Growth in Tested Scenarios:** Validated through 100 rapid open/use/close stress cycles across modules. Memory and OS handle allocations plateau cleanly with .NET garbage collection.
- **Process Termination Cleanliness:** Clean shutdown leaves zero orphaned background processes, helper threads, or leaked Win32 hooks.

### 2.4. Setup & Uninstaller Hardening
- **Single-File Setup:** Modern self-contained `QuickDock-Setup.exe` with embedded payload.
- **Silent Self-Deleting Uninstaller:** Uninstaller worker completely purges installation binaries, Start Menu shortcuts, Desktop icons, and Windows Registry keys, then self-deletes without leaving residual files.

### 2.5. Full UI/UX & Functional Remediation Pass
- **Real Windows WASAPI Audio Endpoint Control:** Replaced placeholder volume logic with true low-level WASAPI `IAudioEndpointVolume` COM interop, enabling master volume level synchronization, real-time volume slider manipulation, and instant audio notifications with 0 thread-marshaling exceptions.
- **Developer Toolkit Repository Discovery:** Implemented intelligent Git repository auto-discovery and integrated a native Windows App SDK `FolderPicker` ("Browse...") dialog. Displays live branch name, recent commits, working directory diffs, and staged status with zero hardcoded paths, plus an honest empty state for non-git folders.
- **Network & Security Diagnostic Suite:** Resolved navigation section mapping across all diagnostic modules (Overview, Adapters, DNS/Ping, Connections, TLS, Headers, Firewall, Security Checks, Integrity, and Reports) with active navigation pill indicators.
- **Screen Time Analytics:** Real foreground application tracking via `IScreenTimeService` and SQLite storage, presenting live usage percentages, daily app time limits, and 7-day sparklines.
- **Interactive Home Cards:** All primary dashboard cards (Screen Time, System, Weather, Focus, Clipboard, Notes) now provide interactive hover border feedback and direct navigation on tap/click.
- **Event-Driven Notch Perimeter Highlight:** Replaced static solid white border with a restrained, hardware-accelerated animated gradient perimeter glow that activates on hover/expand and settles gracefully with 0% idle CPU usage.
- **Clipboard & Notes Empty States:** Added real image thumbnail previews for image clipboard entries and polished empty states for empty history.

---

## 3. Automated Test Verification

| Test Suite | Tests Run | Passed | Failed | Skipped | Duration |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **QuickDock.Core.Tests** | 240 | 240 | 0 | 0 | 20s |

---

## 4. Release Distribution Assets & Cryptographic Hashes

| Artifact | Architecture | File Size | Description | SHA-256 Checksum |
| :--- | :--- | :--- | :--- | :--- |
| **`QuickDock-Setup.exe`** | x64 | **~177 MB** | **Standalone Windows Installer (Recommended)** | `D543711A143E97BAB794BE5844B9E057E8CEE3BDEF320EC36859153E7401D8B6` |
| **`QuickDock-v1.0.1-win-x64.exe`** | x64 | **~177 MB** | Named Standalone Installer | `D543711A143E97BAB794BE5844B9E057E8CEE3BDEF320EC36859153E7401D8B6` |
| **`QuickDock-v1.0.1-win-x64.zip`** | x64 | **~109 MB** | Portable Application Package Archive | `B3D810D6EE656A7113E571757ADACB25C2895167E69877BBB0CC4C40D7EB2CE4` |
| **`SHA256SUMS.txt`** | - | **250 B** | Official Checksum Manifest | See Release Page |

---

## 5. System Requirements

- **Operating System:** Windows 11 (Version 22H2 or higher recommended) or Windows 10 (64-bit, Build 19045+)
- **Architecture:** x64
- **Runtime:** Completely self-contained. No external .NET SDK, .NET Runtime, or Visual Studio installation required.

---

## 6. Author & Attribution

**Developer:** JOJIN JOHN  
**GitHub Profile:** [https://github.com/jojin1709](https://github.com/jojin1709)  
**LinkedIn:** [https://www.linkedin.com/in/jojin-john/](https://www.linkedin.com/in/jojin-john/)  
**License:** [Proprietary License (All Rights Reserved)](../LICENSE)

