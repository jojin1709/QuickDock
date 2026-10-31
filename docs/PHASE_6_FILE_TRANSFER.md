# QuickDock — Phase 6: File & Transfer System Documentation

## Overview

QuickDock Phase 6 introduces high-performance, strictly local file management and device-to-device file transfer capabilities. Operating under the core architecture principle **"CORE STAYS ALIVE. MODULES DO NOT."**, all Phase 6 modules and network services initialize on demand, run asynchronously using memory-safe streaming, enforce rigorous security constraints, and release all OS resources and listening sockets when idle or closed.

All user files and transfer history are maintained **100% locally** on the user's workstation. QuickDock does not connect to any external cloud servers, relays, or telemetry providers.

---

## 1. Architecture & Implemented Modules

### 1.1 File Shelf (`quickdock.fileshelf`)
- **Purpose**: Temporary local file staging area for dragging, opening, previewing, and converting files.
- **Implementation**: `FileShelfService` & `FileShelfViewModel` with `FileShelfView.xaml`.
- **Storage**: References files by existing filesystem path in the `file_shelf` SQLite table. File contents are never duplicated into application directories or databases.
- **Operations**:
  - Drag and drop files and folders from Windows Explorer (`AllowDrop="True"`, `StandardDataFormats.StorageItems`).
  - Drag items back OUT to Explorer (`CanDrag="True"`, `DragStarting`, `SetStorageItems`).
  - Open file with default shell handler (`ProcessStartInfo.UseShellExecute = true`).
  - Reveal in Windows Explorer (`explorer.exe /select,"<path>"`).
  - Copy full path to clipboard (`Windows.ApplicationModel.DataTransfer.Clipboard`).
  - Remove from shelf or clear entire shelf.
  - Delete file with explicit confirmation.
  - Detect missing files if moved or deleted externally (`IsMissing` status) and prune with "Remove Missing".
  - Quick routing: "Send to QuickDrop" and "Send to Converter".

### 1.2 QuickDrop (`quickdock.quickdrop`)
- **Purpose**: Direct device-to-device file transfer over the local Wi-Fi or local area network without cloud servers, user accounts, or mobile apps.
- **Implementation**: `QuickDropService`, `QuickDropHttpServer`, and `QuickDropViewModel` with `QuickDropView.xaml`.
- **Server Lifecycle**:
  - Created and started **only** when the user initiates QuickDrop.
  - Binds directly to the primary active private IPv4 interface (`192.168.x.x`, `10.x.x.x`, `172.16-31.x.x`), avoiding loopback, tunnel, or WAN exposure.
  - Dynamic port selection starting at configurable port (default `48271`), probing until an available port is secured.
  - Shuts down immediately upon user exit, manual stop, or session expiration: closes listening `TcpListener`, aborts active streams, and invalidates tokens.
- **Security & Pairing**:
  - Generates a cryptographically secure 12-byte random hexadecimal pairing token (`RandomNumberGenerator.GetBytes(12)`).
  - Generates an in-memory QR code using `QRCoder` containing the pairing URL (`http://<local-ip>:<port>/?token=<token>`).
  - Every HTTP endpoint (`/`, `/api/info`, `/api/upload`, `/api/download/{fileId}`) requires the valid token. Missing, mismatched, or expired tokens immediately return `403 Forbidden`.
  - **Zero Path Traversal / Arbitrary Path Exposure**: Download endpoints accept only server-generated GUID `fileId`s mapped to explicitly shared files. Clients cannot browse or download arbitrary paths from the Windows PC. Uploaded filenames are sanitized via `Path.GetFileName` to neutralize directory traversal attacks (e.g. `../../evil.exe` is stripped to `evil.exe`).
  - Dedicated upload directory: `%LOCALAPPDATA%\QuickDock\QuickDrop\`.
- **Mobile Web UI**:
  - Embedded, responsive HTML5/CSS3/JavaScript interface.
  - Mobile touch-friendly, dark/light adaptive layout without external dependencies or CDNs.
  - Live upload progress bar using `XMLHttpRequest` upload streaming.
  - Shared files list from Windows with one-click download buttons.

### 1.3 File Transfer Manager (`quickdock.transfers`)
- **Purpose**: Real-time progress monitoring and persistent transfer history across local and network file transfers.
- **Implementation**: `FileTransferManager` and `TransferManagerViewModel` with `TransferManagerView.xaml`.
- **Capabilities**:
  - Live tracking of active transfers (percentage, speed in MB/s, transferred bytes vs total bytes).
  - Cancellation support via transfer IDs.
  - Persistent SQLite history logging (`transfer_history` table) recording direction, file name, size, timestamp, status, and SHA-256 hash.

### 1.4 File Preview (`IFilePreviewService`)
- **Purpose**: Safe local previews without crashing on large files.
- **Implementation**: `FilePreviewService`.
- **Supported Formats**:
  - **Images**: PNG, JPEG, WebP, BMP, GIF, ICO. Parses header signatures safely to extract dimensions (e.g. `1920 × 1080`), file size, and format without loading multi-gigabyte buffers.
  - **Text**: TXT, JSON, XML, CSV, MD, LOG, YAML, INI, CFG, SQL, CS, XAML. Asynchronously streams up to 256 KB. If the file exceeds 256 KB, it reads only the initial slice and sets `IsTruncated = true`, preventing UI freezes or out-of-memory errors on massive log files.
  - **Unsupported**: Flags `PreviewType.Unsupported` gracefully.

### 1.5 File Converter (`quickdock.converter`)
- **Purpose**: Local conversions for images, archives, and text encodings.
- **Implementation**: `FileConversionService` and `FileConverterViewModel` with `FileConverterView.xaml`.
- **Supported Conversions**:
  - **Images**: PNG, JPEG, BMP, GIF. Transcodes between formats with adjustable quality slider (1-100) for JPEG compression. Default behavior preserves the original file and creates a new converted output.
  - **Archives**:
    - Creates standard ZIP archives from files and directory trees.
    - Extracts ZIP archives with **Zip Slip path traversal prevention**: validates every archive entry so that target full paths cannot escape the destination directory (rejecting `../`, `..\`, absolute paths).
  - **Text Encodings**: Safe transcoding between UTF-8, UTF-16 (Unicode), and ASCII with automatic BOM detection.

---

## 2. Database Schema (SQLite Migrations)

Phase 6 adds two new persistent tables to `%LOCALAPPDATA%\QuickDock\quickdock.db` while preserving all existing Phase 5 tables (clipboard, notes, calendar, links, temporary messages, app usage):

```sql
CREATE TABLE IF NOT EXISTS file_shelf (
    id TEXT PRIMARY KEY,
    path TEXT NOT NULL,
    name TEXT NOT NULL,
    size INTEGER NOT NULL,
    type TEXT NOT NULL,
    added_at TEXT NOT NULL,
    last_accessed TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_shelf_added ON file_shelf(added_at);

CREATE TABLE IF NOT EXISTS transfer_history (
    id TEXT PRIMARY KEY,
    direction TEXT NOT NULL,
    filename TEXT NOT NULL,
    size INTEGER NOT NULL,
    timestamp TEXT NOT NULL,
    status TEXT NOT NULL,
    hash TEXT,
    error_message TEXT
);
CREATE INDEX IF NOT EXISTS idx_transfer_timestamp ON transfer_history(timestamp);
```

---

## 3. Security & Privacy Controls

1. **Local Network Boundary**: QuickDrop operates solely over private RFC 1918 IPv4 address ranges (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`). No public WAN interfaces or external sockets are opened.
2. **Cryptographic Tokens**: 96-bit random tokens are required for all HTTP interactions. Tokens are never persisted to disk or logged to files.
3. **Strict Path Sanitization**:
   - `Path.GetFileName` is applied to all remote upload filenames.
   - ZIP extraction explicitly guards against Zip Slip attacks, throwing `SecurityException` if an entry traverses outside the target directory.
4. **Data Purge**: Settings view provides "Clear All Stored Data", which clears the `file_shelf` and `transfer_history` tables alongside all Phase 5 data.

---

## 4. Verification and Automated Testing

### 4.1 Automated Test Suites
Added in `tests/QuickDock.Core.Tests/`:
- **`FileHashTests.cs`** (3 tests):
  - Validates streaming SHA-256 computation against known hashes.
  - Validates progress reporting during large file hashing.
  - Validates exception handling on non-existent files.
- **`FileShelfTests.cs`** (3 tests):
  - Adding files to shelf and verifying metadata persistence in SQLite.
  - Detecting externally deleted files and purging missing items.
  - Clearing the shelf.
- **`FileTransferTests.cs`** (2 tests):
  - Full transfer lifecycle (register -> progress update -> complete) and SQLite history recording.
  - Cancelling active transfers.
- **`FileConverterTests.cs`** (5 tests):
  - Image conversion (PNG -> JPEG with quality adjustment).
  - Unsupported format handling.
  - Safe ZIP creation and extraction.
  - **Zip Slip protection**: verifies that entries like `../../evil_payload.bat` throw `SecurityException`.
  - Text encoding conversion between UTF-8 and ASCII.
- **`QuickDropSecurityTests.cs`** (6 tests):
  - Rejection of requests without token (`403 Forbidden`).
  - Rejection of requests with invalid token (`403 Forbidden`).
  - Rejection of requests after session expiration (`403 Forbidden`).
  - Successful response with valid pairing token.
  - Rejection of downloads for non-shared `fileId`s (`404 Not Found`).
  - Sanitization of upload filenames attempting path traversal (`../../evil.exe` -> `evil.exe`).
- **`FileModuleLifecycleTests.cs`** (2 tests):
  - Lifecycle states (`Uninitialized` -> `Initialized` -> `Running` -> `Stopped`) and idempotency across all four Phase 6 modules.
  - `QuickDropService` duplicate start protection and clean listener shutdown.

### 4.2 Full Test Run Results
```text
Passed!  - Failed: 0, Passed: 109, Skipped: 0, Total: 109, Duration: 1 s - QuickDock.Core.Tests.dll (net10.0)
```

### 4.3 Build Verification
```text
dotnet build --no-incremental
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## 5. Known Limitations
- QuickDrop utilizes HTTP on the local Wi-Fi network rather than HTTPS with custom CA certificates to ensure friction-free pairing across mobile browsers without SSL certificate warning prompts. The temporary cryptographic token restricts pairing access exclusively to devices with the QR code.
- File preview for text documents safely truncates display at 256 KB to ensure predictable WinUI 3 memory consumption.
