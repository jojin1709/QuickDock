# QuickDock — Phase 5: Productivity Modules Documentation

## 1. Executive Summary
Phase 5 ("Productivity Modules") delivers 10 high-frequency, fully integrated productivity utilities into QuickDock:
1. **Clipboard Manager** (multi-format history, regex sensitive-data classification, search, pinning, pruning)
2. **Timer** (multiple concurrent countdowns, preset buttons, completion audio/toasts)
3. **Stopwatch** (monotonic high-resolution precision timer with split/lap recording)
4. **Pomodoro** (structured focus intervals with automated work/break transitions and session tracking)
5. **Notes** (local-first scratchpad with debounced autosave, instant search, and pinning)
6. **Calendar** (month-by-month agenda, date selection, local event planning)
7. **Weather** (provider abstraction with Open-Meteo, automatic geocoding, two-tier cache, offline fallback)
8. **Saved Links** (safe bookmark manager launching default browser via validated HTTP/HTTPS protocols)
9. **Temporary Message** (expiring activity reminders with auto-pruning)
10. **Screen Time / Application Usage** (low-frequency foreground window tracking, daily/weekly aggregation, privacy controls)

In accordance with QuickDock's core architecture guideline **"CORE STAYS ALIVE. MODULES DO NOT."**, all modules activate resources only on demand and release resources upon deactivation.

---

## 2. Database Schema (`quickdock.db`)

Managed via `SqliteDatabaseService` with WAL (`Write-Ahead Logging`) mode, foreign keys, and parameterized queries:

### Tables & Indexes

```sql
-- 1. Clipboard Items
CREATE TABLE IF NOT EXISTS clipboard_items (
    id TEXT PRIMARY KEY,
    type INTEGER NOT NULL,          -- 0=Text, 1=Url, 2=Image, 3=Files
    content TEXT NOT NULL,
    preview TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    is_pinned INTEGER NOT NULL DEFAULT 0,
    is_sensitive INTEGER NOT NULL DEFAULT 0,
    image_path TEXT
);
CREATE INDEX IF NOT EXISTS idx_clipboard_created ON clipboard_items(created_at);
CREATE INDEX IF NOT EXISTS idx_clipboard_pinned ON clipboard_items(is_pinned);

-- 2. Notes
CREATE TABLE IF NOT EXISTS notes (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    content TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    is_pinned INTEGER NOT NULL DEFAULT 0,
    is_archived INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_notes_updated ON notes(updated_at);
CREATE INDEX IF NOT EXISTS idx_notes_pinned ON notes(is_pinned);

-- 3. Calendar Events
CREATE TABLE IF NOT EXISTS calendar_events (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    description TEXT NOT NULL,
    event_date TEXT NOT NULL,
    start_time TEXT NOT NULL,
    end_time TEXT NOT NULL,
    created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_calendar_date ON calendar_events(event_date);

-- 4. Saved Links
CREATE TABLE IF NOT EXISTS saved_links (
    id TEXT PRIMARY KEY,
    title TEXT NOT NULL,
    url TEXT NOT NULL,
    description TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    is_pinned INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_links_updated ON saved_links(updated_at);

-- 5. Temporary Messages
CREATE TABLE IF NOT EXISTS temporary_messages (
    id TEXT PRIMARY KEY,
    content TEXT NOT NULL,
    created_at TEXT NOT NULL,
    expires_at TEXT NOT NULL,
    is_dismissed INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_temp_msg_expires ON temporary_messages(expires_at);

-- 6. Screen Time / Application Usage
CREATE TABLE IF NOT EXISTS app_usage (
    id TEXT PRIMARY KEY,            -- deterministic '{YYYY-MM-DD}_{AppName}'
    usage_date TEXT NOT NULL,
    app_name TEXT NOT NULL,
    window_title TEXT NOT NULL,
    duration_seconds INTEGER NOT NULL DEFAULT 0,
    updated_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_usage_date_app ON app_usage(usage_date, app_name);
```

---

## 3. Privacy & Data Protection Model

1. **Zero External Sync / Zero Telemetry:**
   - No user content (clipboard, notes, URLs, calendar events, screen time) is transmitted over network or uploaded anywhere.
2. **Local Sensitive Data Classifier (`SensitiveDataDetector`):**
   - High-confidence pattern matching for AWS access keys, GitHub personal access tokens, OpenAI/generic secret keys, JWTs, and private keys.
   - Configurable behavior (`SensitiveDataHandling.Ignore`, `StoreNormally`, `RequireConfirmation`). When set to `Ignore`, secrets are never committed to the database.
3. **Clipboard Retention & Pausing:**
   - User can pause clipboard recording at any time from UI or settings.
   - Retention policy automatically purges unpinned entries exceeding configured age (1 hour to 30 days) or count limits.
4. **Screen Time Isolation:**
   - Low-frequency sampling (2-second interval) of foreground window only.
   - Tracking can be fully disabled in Settings.
   - Does not log keystrokes, window contents, or screenshots.
5. **Clear Data Controls:**
   - Settings exposes "Clear All Stored Data" and "Clear History" options executing parameterized deletion directly against SQLite.

---

## 4. Windows APIs & Platform Integration

| Capability | Windows API / Interop Mechanism | Purpose |
|---|---|---|
| **Clipboard Listener** | `Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged` | Event-driven notification without polling loops. |
| **Clipboard Extraction** | `Windows.ApplicationModel.DataTransfer.Clipboard.GetContent()` | Standard format extraction for text, web links, bitmap snapshots, and storage items. |
| **Stopwatch Timing** | `System.Diagnostics.Stopwatch.GetTimestamp()` | Monotonic high-resolution timer immune to UI dispatcher jitter. |
| **Foreground Sampling** | Win32 `GetForegroundWindow`, `GetWindowThreadProcessId`, `GetWindowTextW` | Active window and process identification. |
| **Audio Cues** | Win32 `MessageBeep` (`MB_ICONEXCLAMATION`, `MB_ICONASTERISK`) | Lightweight native audio notification for timers and Pomodoro. |
| **Link Launching** | `Process.Start(new ProcessStartInfo { UseShellExecute = true })` | Safe protocol launch in the user's default browser. |

---

## 5. Weather Provider & Caching Architecture

- **Provider:** `IWeatherProvider` -> `OpenMeteoWeatherProvider` (free, open, no mandatory API key required).
- **Geocoding:** Automatic location resolution through Open-Meteo Geocoding API (`https://geocoding-api.open-meteo.com/v1/search?name={city}&count=1`).
- **Two-Tier Caching:**
  - In-memory cache checked before executing network requests.
  - Disk persistence (`%LOCALAPPDATA%\QuickDock\Cache\weather_cache.json`) for instant startup and offline fallback.
  - Configurable refresh interval (default 30 minutes). If offline or network errors occur, cached data is presented with a "Cached" badge.

---

## 6. Performance Observations

- **Idle:** Core + UI shell maintain 0.0% CPU usage.
- **Home View:** Displays lightweight summaries (recent clip preview, active timer text, weather summary, screen time total) without loading full module views.
- **Background Timers:** Central ticker runs only when at least one timer or Pomodoro is actively running, and is immediately disposed when all timers are paused/stopped.
- **Screen Time:** 2-second low-frequency sampling runs only while tracking is enabled.

---

## 7. Test Results

- Total Tests: **88 passed**, **0 failed**, **0 skipped**.
- Test Suites:
  - `ClipboardTests` (Sensitive data regexes, CRUD, search, pin, retention pruning)
  - `TimerStopwatchPomodoroTests` (Multiple timers, countdowns, laps, Pomodoro phases)
  - `NotesAndLinksTests` (Note CRUD, autosave debouncing, search, pin, archive; link validation and search)
  - `CalendarAndTempMessageTests` (Event date/month filtering, next event, temporary message expiration)
  - `WeatherAndScreenTimeTests` (WMO code mapping, cache fallback, screen time daily/weekly aggregation)
  - `ProductivityModuleLifecycleTests` (8 modules tested through Initialize, Start, Stop, duplicate start/stop idempotency)
  - Previous Phase 1–4 tests (Metrics, Core audio, Media controls, Fullscreen hook, Monitor service, Clock)
