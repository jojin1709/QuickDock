# Changelog

All notable changes to **QuickDock** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0] - 2026-11-01

### Official Production Release

#### Added
- **Native Windows 11 Shell & Architecture**:
  - Razor-thin top-center notch/island dock window with acrylic backdrop and smooth spring animations.
  - Global hotkey toggle (<kbd>Ctrl</kbd> + <kbd>Space</kbd>) via Win32 `RegisterHotKey`.
  - System tray icon with context menu and background minimization.
  - Edge hover trigger with configurable activation zone and delay.
  - Multi-monitor tracking with dynamic DPI scaling (`GetDpiForWindow`).
  - Fullscreen detection suppressing dock intrusions during gaming or presentations.
  - Single-instance enforcement via Windows named mutex and IPC window activation messages.
- **System Metrics & Audio Control**:
  - Real-time CPU, RAM, GPU, Disk, and Battery hardware monitors via Win32 performance counters and `GlobalMemoryStatusEx`.
  - Core Audio (WASAPI) master volume slider with mute toggle and dynamic audio session device enumeration.
  - Global System Media Transport Controls (GSMTC) player with track metadata, thumbnail, play/pause/prev/next controls.
- **Productivity Suite**:
  - Clipboard History manager with sensitive key masking (AWS, GitHub, OpenAI, JWT, private keys) and local SQLite persistence.
  - Pomodoro timer with 25/5/15 intervals and audible cues.
  - Monotonic countdown timer and high-resolution stopwatch.
  - Notes scratchpad with auto-save and category filtering.
  - Calendar agenda with event scheduling.
  - Offline-first weather forecast via Open-Meteo API.
  - Bookmarks library with default browser dispatch.
  - Temporary message banner with 1h/4h/24h expiration.
  - Screen Time tracker with foreground window sampling.
- **File & Transfer System**:
  - File Shelf staging area supporting drag-and-drop file pinning.
  - QuickDrop zero-cloud peer-to-peer file transfer engine:
    - In-process HTTP server binding to local private Wi-Fi network interfaces.
    - QR code pairing with mobile browser companion interface.
    - Timing-safe cryptographic pairing tokens (`CryptographicOperations.FixedTimeEquals`).
    - DOS reserved device name sanitization (`CON`, `PRN`, `AUX`, `NUL`, etc.).
    - Directory escape assertion and 5 GB payload bounding.
  - Streaming SHA-256 hash validation and transfer history logging.
  - Image conversion utilities (`PNG`, `JPEG`, `BMP`, `GIF`) and Zip-Slip & Zip-Bomb protected archive extraction.
- **Developer Toolkit**:
  - Git repository inspector (status, branch, staged/unstaged changes, commit, diff viewer).
  - Docker container manager (status, start, stop, restart, logs viewer).
  - Terminal launcher (Windows Terminal, PowerShell, CMD) with sanitized working directory execution.
  - Offline developer converters: JSON formatter/validator, JWT decoder, Base64/URL, SHA/MD5 hashes, UUID v4 generator, Unix timestamp converter, Regex tester, QR code generator.
  - HTTP REST client with JSON formatting and 5 MB memory exhaustion bounding.
  - Localhost & listening socket inspector via Win32 `GetExtendedTcpTable`.
  - Developer process monitor with memory inspection and protected process safeguards (PID <= 4 and host PID).
- **Network & Defensive Security Diagnostics**:
  - Network overview with interface routing, adapter speed, and IP routing tables.
  - DNS diagnostics via Win32 `DnsQuery_W` (`dnsapi.dll`) with latency timing.
  - ICMP ping, MTU path test, and stepped TTL traceroute.
  - Active TCP/UDP socket table with owning process mapping.
  - ARP neighbor table and IP route inspector.
  - Direct TLS/SSL handshake inspector with X.509 certificate chain validation and cipher suite display.
  - HTTP security header auditor (HSTS, CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, Permissions-Policy).
  - Public IP and RDAP autonomous system lookup (user-consented).
  - Windows Defender service state and Windows Firewall profile auditor.
  - Security posture baseline checker (UAC, BitLocker, Secure Boot, Guest accounts).
  - Cryptographic file integrity baseline hashing and tamper detection.
  - Security audit report generator exporting structured Markdown and JSON reports.
- **Settings & Personalization**:
  - 12 comprehensive configuration categories (Dock, Appearance, Modules, Startup, Hotkeys, Notifications, Privacy, Storage, Network, Developer, Accessibility, About).
  - Windows App SDK backdrop switching: Mica, Mica Alt, Acrylic, and Solid.
  - Storage inspector with cache clearing and database optimization (`VACUUM`).
  - Full configuration backup and restoration via JSON export/import.
- **Performance & Reliability**:
  - Core architectural principle enforced: **CORE STAYS ALIVE. MODULES DO NOT.**
  - Zero-polling idle execution when dock is collapsed.
  - Lazy on-demand module initialization.
  - Bounded child process execution with guaranteed process tree termination.
  - SQLite Write-Ahead Logging (`WAL`) mode with automatic corruption self-healing (error 11/26).
  - Global UI and TaskScheduler unhandled exception containment boundaries.
- **Packaging & Deployment**:
  - Self-contained, unpackaged Windows 11 x64 distribution.
  - Zero developer machine path dependencies.
  - Clean `%LOCALAPPDATA%\QuickDock\` storage model with graceful upgrade and uninstall support.
  - 224 automated unit and security tests passing with 0 warnings and 0 errors.

[1.0.0]: https://github.com/jojin1709/QuickDock/releases/tag/v1.0.0
