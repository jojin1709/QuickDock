# QuickDock — Phase 4: Core Utility Modules Implementation & Verification Report

## Executive Summary
Phase 4 ("Core Utility Modules") establishes real hardware-integrated system telemetry, multimedia audio/playback control, clock formatting, and network/power monitoring into QuickDock.
In strict alignment with the core architecture guideline **"CORE STAYS ALIVE. MODULES DO NOT."**, all modules execute with lazy activation: background polling and hardware sampling are initiated only when the module/view is active, and immediately terminated upon deactivation or minimization, maintaining zero idle CPU overhead.

All metrics are sourced directly from Windows 11 platform APIs with COM interop and Win32 P/Invoke. **No mock data, random numbers, or placeholder metrics exist anywhere in the codebase.**

---

## Implemented Modules & Services

### 1. System Metrics Service (`QuickDock.Services.System`)
- **Interface:** `ISystemMetricsService`
- **Implementation:** `SystemMetricsService`
- **Metrics Collected:**
  - **CPU Utilization:** Calculated via Win32 `GetSystemTimes` differential kernel, user, and idle time computations across multi-core architectures.
  - **Physical RAM:** Queried via Win32 `GlobalMemoryStatusEx` (`TotalPhys`, `AvailPhys`, and load percentage).
  - **GPU & VRAM:** Native DirectX DXGI interop (`CreateDXGIFactory1`, `IDXGIFactory1`, `IDXGIAdapter1`, and `IDXGIAdapter3.QueryVideoMemoryInfo` with `DXGI_MEMORY_SEGMENT_GROUP_LOCAL`). Retrieves GPU hardware description (e.g. NVIDIA GeForce RTX 3050 Laptop GPU / Intel Iris Xe) and exact VRAM usage in megabytes.
  - **Storage:** Queried via `System.IO.DriveInfo` for root drive (`C:\`), reporting used space, total space, and utilization percentage.
  - **Battery & AC Power:** Queried via Win32 `GetSystemPowerStatus` (`ACLineStatus`, `BatteryLifePercent`, `BatteryFlag`).
  - **Network Status:** Queried via `Windows.Networking.Connectivity.NetworkInformation.GetInternetConnectionProfile()` and `System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()`. Reports connection state and adapter name (e.g. Wi-Fi / Ethernet).
  - **System Uptime:** Calculated via `Environment.TickCount64` formatted into days, hours, and minutes.
- **Resource Management:** `GetSnapshot()` evaluates current metrics on demand. Real-time background timer runs only when subscribed via `SystemModule` or when `SystemViewModel.StartLiveUpdates()` is explicitly engaged.

### 2. Audio Endpoint Volume Service (`QuickDock.Services.Audio`)
- **Interface:** `IAudioService`
- **Implementation:** `CoreAudioService`
- **Windows API:** Windows Core Audio API (WASAPI) via COM Interop:
  - `IMMDeviceEnumerator` (`CLSID_MMDeviceEnumerator`, `IID_IMMDeviceEnumerator`)
  - `IMMDevice` (`eRender`, `eConsole`)
  - `IAudioEndpointVolume` (`IID_IAudioEndpointVolume`)
  - `IAudioEndpointVolumeCallback` for event-driven reactive updates when hardware buttons or external apps adjust volume or mute state.
  - `IPropertyStore` (`PKEY_Device_FriendlyName`) for discovering the default audio output endpoint name.
- **Features:** Master volume level get/set (0–100%), mute toggle, step up (+5%), step down (-5%), reactive notifications without polling.

### 3. Media Transport Control Service (`QuickDock.Services.Media`)
- **Interface:** `IMediaService`
- **Implementation:** `WindowsMediaService`
- **Windows API:** `Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager`
- **Features:**
  - Event-driven notifications via `CurrentSessionChanged` and `PlaybackInfoChanged`.
  - Media session extraction: Title, Artist, Album, and playback status (`Playing`, `Paused`, `Stopped`).
  - Transport controls: `PlayPauseAsync()`, `SkipNextAsync()`, `SkipPreviousAsync()`.
  - Graceful fallback when no active playback session exists ("No media playing").

### 4. Clock Module (`QuickDock.Modules.Clock`)
- **ID:** `quickdock.clock`
- **Capabilities:**
  - Time format configurable between 12-hour (with AM/PM) and 24-hour formats.
  - Optional seconds display toggle (`ShowSeconds`).
  - Formatted localized date string (e.g. "Saturday, October 31, 2026").
  - TimeZone selection support (defaults to local system time zone).

### 5. Dedicated Battery & Network Modules
- **Battery Module (`quickdock.battery`):** Exposes battery percentage, charging state, and icon hints.
- **Network Module (`quickdock.network`):** Exposes connection state (Online/Offline) and adapter name.

---

## UI Shell Integration & Views

### Home Dashboard (`HomeView.xaml` & `HomeViewModel.cs`)
- **Master Volume Card:**
  - Interactive volume slider with live percentage readout.
  - Mute/Unmute toggle button reflecting active audio state.
  - Dynamic audio endpoint friendly name.
- **Now Playing Media Card:**
  - Displays current media title and artist.
  - Interactive playback buttons: Previous Track, Play/Pause toggle, Next Track.
- **System Metrics Bar:**
  - Live CPU %, RAM %, and Battery % pills in the top status bar.
- **Module Grid Tiles:**
  - Interactive tiles for System, Clock, Volume, Media, Battery, and Network.
  - Clicking the **System** tile smoothly navigates to the detailed System View.

### Detailed System Monitor View (`SystemView.xaml` & `SystemViewModel.cs`)
- Dedicated full-view telemetry dashboard with modern Fluent WinUI 3 cards:
  - **Processor (CPU):** Utilization percentage and live progress bar.
  - **Physical Memory (RAM):** Used vs. total gigabytes and percentage load.
  - **Dedicated Graphics (GPU):** Hardware adapter name and dedicated VRAM usage.
  - **System Storage (C:):** Used space, free space, and drive utilization.
  - **Power & Battery:** Battery percentage, AC line charging indicator.
  - **Network Connectivity:** Connection status (Internet access / Disconnected) and interface name.
  - **System Uptime:** Formatted continuous system uptime.
  - **Navigation:** Back button returning to the Home view.
- **Lifecycle Guarantees:** Polling timer starts strictly on `StartLiveUpdates()` when the view is navigated to, and stops immediately via `StopLiveUpdates()` upon leaving or closing the panel.

---

## Quality & Verification

### Test Coverage (`tests/QuickDock.Core.Tests`)
1. `ClockFormattingTests`:
   - 12-hour format with AM/PM.
   - 24-hour format.
   - Seconds toggle verification.
   - Date format verification.
   - Custom timezone conversion.
2. `MetricsCalculationTests`:
   - RAM usage calculation and bounding (0–100%).
   - Disk space and utilization percentage.
   - Battery charging and status reporting.
   - Network status and adapter naming.
   - System uptime formatting.
3. `VolumeServiceTests`:
   - Clamping volume between 0.0 and 1.0 (0% - 100%).
   - Step adjustment increments (+5%, -5%).
   - Mute toggle state preservation.
4. `MediaServiceTests`:
   - Playback status mapping.
   - Graceful fallback for empty sessions.
5. `ModuleLifecycleTests`:
   - Safe transitions: `Uninitialized` -> `Initialized` -> `Running` -> `Stopped`.
   - Redundant start/stop idempotency.
   - Thread-safe state locks.

