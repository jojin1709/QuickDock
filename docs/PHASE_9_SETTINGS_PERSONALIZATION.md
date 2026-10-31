# QuickDock — Phase 9: Settings & Personalization Technical Specification

## 1. Architectural Overview

QuickDock Phase 9 delivers a comprehensive, native Windows 11 configuration and personalization architecture without compromising the foundational system law:

> **CORE STAYS ALIVE. MODULES DO NOT.**

Settings in QuickDock do not incur runtime performance penalties, background timers, or permanent sensor polling. Setting mutations operate on strongly typed in-memory models backed by thread-safe, atomic JSON persistence and reactive in-memory event bus broadcasting (`SettingsChangedEvent`).

```
Settings Navigation
├── General
├── Appearance
├── Dock
├── Modules
├── Shortcuts
├── Notifications
├── Privacy
├── Storage
├── Network
├── Developer
├── Accessibility
└── About
```

---

## 2. Configuration Persistence & Safety Architecture

Settings persist in `%LOCALAPPDATA%\QuickDock\config.json` through `ISettingsManager` / `JsonSettingsManager`:

1. **Atomic File Replacement**: New configurations are serialized to a temporary GUID-named file (`config.json.{guid}.tmp`), flushed to disk, and atomically moved via `File.Move(temp, target, overwrite: true)`. Sudden power loss or crashes cannot corrupt existing settings.
2. **Crash Resilience & Fallback**: If `config.json` becomes corrupt or contains malformed JSON, `JsonSettingsManager` logs a warning and falls back to safe default configuration without terminating the host process.
3. **Reactive Notification**: Any persisted mutation automatically publishes a `SettingsChangedEvent(settings, changedSection)` across `IEventBus`. UI subscribers (such as `MainWindow` and active views) dynamically update theme, window positioning, or top-most attributes in real time without restarting.

---

## 3. Settings Information Architecture (12 Functional Sections)

### 3.1. General
- **Start with Windows**: Integrates directly with `IStartupService` via Windows HKCU Run registry key without duplicating logic.
- **Start Minimized**: Launches QuickDock into its compact idle dock state.
- **Launch Behavior**: `ShowDock`, `StartMinimized`, or `StartHidden`.
- **Close Behavior**: `MinimizeToTray` or `ExitApplication`.

### 3.2. Appearance
- **Theme**: System Default, Light mode, or Dark mode with real-time WinUI 3 `RequestedTheme` synchronization.
- **Accent Palette**: Six restrained presets (`Blue #0078D4`, `Purple #8764B8`, `Green #107C41`, `Orange #CA5010`, `Red #D13438`, and Custom Hex).
- **Backdrop Material**: Dynamic selection between `Mica`, `Acrylic`, and `Solid` backdrops with graceful hardware fallback.
- **Controlled Transparency**: `Off` (Opaque) or `Subtle` (Controlled alpha composition).
- **Animation Motion**: `System Default`, `Reduced Motion`, or `Off`.

### 3.3. Dock Positioning & Sizing
- **Position**: `Top` or `Bottom` display docking. `WindowPositioningService` calculates work area bounds and flips vertical offsets accordingly.
- **Capsule Sizing**: `Compact` (85%), `Normal` (100%), and `Large` (115%) dimensions.
- **Hover Expansion**: Master toggle enabling or disabling pointer hover expansion.
- **Hover Delay**: Bounded 50 ms – 1000 ms delay with fine-grained slider control.
- **Always on Top**: Toggles Win32 `HWND_TOPMOST` / `HWND_NOTOPMOST` dynamically.
- **Auto-Hide Policy**: `Never`, `Fullscreen Only`, or `Always`.
- **Target Display**: Seamless multi-monitor target selection (Follow Active Window, Primary Monitor, or specific monitor device ID).

### 3.4. Module Lifecycle Management
- Displays the complete catalog grouped into **Productivity**, **Files & Transfer**, **Developer**, and **Network & Security**.
- Disabling a module:
  - Halts active timers and background sockets.
  - Releases module memory.
  - Suppresses initialization on subsequent boots.
- Re-enabling a module initializes the module on demand. Core utility infrastructure remains unaffected.

### 3.5. Keyboard Shortcuts & Conflict Detection
- Centralized `IShortcutManagerService` tracks keybindings for `Open QuickDock` (`Ctrl+Space`), `New Timer`, `Open Clipboard`, `Open Notes`, `Open Dev Toolkit`, and `Open Network & Security`.
- **Conflict Detection**: Reject duplicate key assignments before persisting.
- **Format Validation**: Requires at least one modifier key (`Ctrl`, `Shift`, `Alt`, `Win`) and a valid virtual key.
- **One-Click Reset**: Restores original defaults.

### 3.6. Notifications
- Master toggle for all notifications.
- Category filters: Timer alerts, Pomodoro transition chimes, QuickDrop pairings, Transfer progress, and Security audit notices.
- `NotificationService` automatically checks category preferences before emitting shell tray balloon toasts.

### 3.7. Privacy & Local-First Guarantees
- Explicit permission switches for external requests:
  - Weather forecasts (Open-Meteo)
  - Public IP & RDAP lookups
  - HTTP header inspection
  - DNS diagnostics
- **Usage Data**: Hardcoded zero telemetry. Toggle is disabled and labeled "Not currently implemented" by architectural design.
- **Sensitive Data Shield**: Governs clipboard history token masking for OpenAI keys, AWS keys, GitHub tokens, and JWTs.

### 3.8. Storage Management
- Real-time disk size breakdown for:
  - Cache directory
  - Logs directory
  - SQLite database
  - Temporary QuickDrop staging files
  - Total storage footprint
- Actions: `Clear Cache`, `Clear Logs`, `Clear Temp Files`, `Clear Productivity Data` (Notes, Calendar, Screen Time), and `Open Data Folder` in Windows Explorer.

### 3.9. Network Preferences
- Configurable timeout thresholds:
  - General request timeout (5s – 120s)
  - DNS query timeout (1s – 30s)
  - HTTP / TLS inspection timeout (2s – 60s)
- Architectural confirmation: All network diagnostics execute on demand; zero background socket listeners or port sweeps.

### 3.10. Developer Preferences
- Logging verbosity: `Information`, `Debug`, `Warning`, `Error`.
- Developer diagnostics and runtime benchmark flags.
- Process Monitor refresh rate interval (clamped 2s – 30s).
- Default Git repository workspace and destructive Git action confirmations.

### 3.11. Accessibility
- Reduced motion toggle with system preference inheritance.
- UI text scaling multipliers: `100%`, `110%`, `125%`, and `150%`.
- High contrast accessibility awareness.
- Master toggle for hover guidance tooltips.

### 3.12. About & Updates
- Application version: `1.0.0 (Phase 9 Personalization)`.
- Developer attribution: `JOJIN JOHN`.
- License: `MIT License`.
- Official GitHub repository and architectural documentation links.
- "Check for Updates": Clearly surfaces `"Update checking will be available in the release system."` without simulated update downloads.

---

## 4. Import, Export, & Reset Subsystem

### 4.1. Export Format (Schema v1)
Settings exports produce clean, human-readable JSON:
```json
{
  "schemaVersion": 1,
  "application": "QuickDock",
  "exportedAtUtc": "2026-10-31T18:15:00Z",
  "settings": { ... }
}
```

### 4.2. Sensitive Field Exclusion
During export, all private credentials are systematically scrubbed:
- `Weather.ApiKey` is stripped.
- Any keys in `CustomModuleConfig` containing `token`, `secret`, `key`, or `password` are removed.
- Database contents and clipboard data are never included in configuration exports.

### 4.3. Import Validation & Hardening
- **Schema Version Verification**: Verifies `schemaVersion == 1`.
- **Payload Size Threshold**: Rejects files larger than 5 MB to prevent memory exhaustion.
- **Boundary Clamping**: Restricts imported numeric fields (e.g. `HoverDelayMs` clamped to 50–2000 ms; `RequestTimeoutSeconds` clamped to 5–120 s).
- **Execution Safeguard**: No commands or arbitrary script paths from configuration are ever executed.

### 4.4. Granular Reset & Data Preservation
- Users can reset individual sections (e.g. Reset Appearance, Reset Dock, Reset Shortcuts).
- "Reset All Settings" restores configuration to defaults.
- **Preservation Boundary**: Settings resets strictly touch `config.json`. SQLite tables, user notes, calendar events, bookmarks, file shelf references, and transfers are permanently preserved.

---

## 5. Settings Search Engine

The Settings view features a real-time search engine:
- Pre-indexes setting titles, descriptions, section keys, and related search keywords.
- As the user types, matches across all 12 sections are dynamically surfaced.
- Selecting a search result navigates directly to the target section in the sidebar.

---

## 6. Verification & Test Matrix

| Test Category | Tested Capabilities | Status |
| :--- | :--- | :--- |
| **Settings Lifecycle** | Default initialization, load, atomic save, crash fallback | Passed |
| **Appearance & Dock** | Theme, accent, position (Top/Bottom), sizing (Compact/Large) | Passed |
| **Module Management** | Enable/disable state, lifecycle callbacks, persistence | Passed |
| **Keyboard Shortcuts** | Gesture normalization, conflict detection, duplicate rejection, reset | Passed |
| **Notifications** | Master toggle suppression, category-specific filtering | Passed |
| **Storage Diagnostics** | Size calculation, cache clearing, log clearing, temp file removal | Passed |
| **Import / Export** | Version validation, JSON deserialization, credential scrubbing, size capping | Passed |
| **Granular Reset** | Individual section reset, full reset, productivity data preservation | Passed |
| **Runtime Verification** | WinUI 3 process launch, window activation, clean exit | Passed |

**Overall automated test suite: 184 Passed, 0 Failed, 0 Skipped.**

