# QuickDock — Phase 7: Developer Toolkit

## 1. Overview & Architecture

Phase 7 integrates a complete, production-grade **Developer Toolkit** directly into the QuickDock WinUI 3 desktop shell. It equips software engineers and developers with daily developer essentials on Windows 11 without requiring bloated, electron-based utilities or third-party web services.

All developer modules strictly adhere to the QuickDock architecture principle:
> **"CORE STAYS ALIVE. MODULES DO NOT."**

Developer services and views are lazy-loaded, activated on-demand, and dormant when not in active use. There are **zero** permanent background Git watchers, **zero** continuous Docker polling loops, **zero** persistent HTTP servers, and **zero** embedded terminal background processes.

---

## 2. Implemented Toolkits & Capabilities

### 1. Git Toolkit (`quickdock.git`)
- **Repository Detection**: Automatically identifies if the working directory or chosen folder is a valid Git repository using `git rev-parse --is-inside-work-tree`.
- **Status & Working Tree**: Displays the current branch, head commit, and working tree files partitioned into Modified, Added, Deleted, Renamed, and Untracked (`git status --porcelain=v1 -b`).
- **Diff Viewer**: Displays unstaged and staged code diffs using safe `ProcessStartInfo.ArgumentList` invocation.
- **Git Operations**: Stage File (`git add -- <file>`), Stage All (`git add -A`), Unstage File (`git restore --staged -- <file>`), Commit (`git commit -m <msg>`), Fetch (`git fetch`), Pull (`git pull`), Push (`git push`), and Discard Changes (`git restore -- <file>`).
- **Safety**: Destructive actions (e.g. discarding changes) require explicit confirmation. Arguments are passed as dedicated argument lists to eliminate shell command injection vulnerabilities.

### 2. Docker Toolkit (`quickdock.docker`)
- **Engine Status**: Queries Docker CLI version, engine running/stopped state, and connection status via `docker version --format "{{json .}}"`.
- **Container Management**: Lists all local containers with short ID, name, image, state (running/exited), status description, and port mappings.
- **Container Lifecycle**: Start (`docker start`), Stop (`docker stop`), Restart (`docker restart`), View Logs (`docker logs --tail 100`), Inspect, and Remove (`docker rm -f`).
- **Image Inspector**: Lists local images with repository, tag, size, and ID; allows image removal (`docker rmi`).
- **Graceful Degradation**: If Docker Desktop is not installed or the daemon is stopped, a clear status message is surfaced without UI crashes or fake data.

### 3. Terminal Launcher (`quickdock.terminal`)
- **Supported Terminals**:
  - Windows Terminal (`wt.exe`)
  - PowerShell (`pwsh.exe` or `powershell.exe`)
  - Command Prompt (`cmd.exe`)
- **Working Directory Flexibility**: Launch at the current QuickDock folder, the active Git repository, or any user-defined folder.
- **Elevation Support**: Allows launching terminals with Administrator privileges (`runas` verb).

### 4. JSON Toolkit (`quickdock.json`)
- **Formatting & Indentation**: Formats compact or messy JSON with 2-space indentation using `System.Text.Json.Utf8JsonWriter`.
- **Minification**: Compresses JSON payloads by removing superfluous whitespace.
- **Syntax Validation**: Validates JSON and pinpoints syntax errors, including exact line number, column byte position, and error descriptions.
- **Large Payload Safety**: Uses streaming/document parsers to prevent UI hangs on large JSON payloads.

### 5. JWT Toolkit (`quickdock.jwt`)
- **Header & Claims Decoding**: Decodes Base64Url-encoded header and payload objects, formatting them into human-readable JSON.
- **Claim Extraction**: Extracts standard JWT claims including `sub` (subject), `iss` (issuer), `aud` (audience), `exp` (expiration), `nbf` (not before), and `iat` (issued at).
- **Expiration Status**: Evaluates token validity against current UTC time to display an active or expired indicator.
- **Security & Integrity Distinction**:
  - Distinctly marks tokens as **`DECODED`** vs **`VERIFIED`**.
  - Prominently displays the warning: *"Decoding a JWT does not verify its signature."*
  - Provides optional local HMAC-SHA256 signature verification if a secret key is provided.
  - Zero external calls; token data never leaves the local machine.

### 6. Encoding Toolkit (`quickdock.encoding`)
- **Base64**: Safe encoding and decoding with graceful handling of malformed padding or invalid characters without throwing unhandled exceptions.
- **URL Encoding**: RFC 3986 URI component encoding and decoding.
- **Text Encodings**: Cross-converts strings between UTF-8, UTF-16, ASCII, and Latin1.

### 7. Hashing Toolkit (`quickdock.hash`)
- **Supported Digest Algorithms**: MD5, SHA-1, SHA-256, SHA-384, SHA-512.
- **Text Hashing**: Instant cryptographic hashing of user strings with execution timing in milliseconds.
- **Streaming File Hashing**: Reads large files in 64 KB chunks using `IncrementalHash` to guarantee low memory usage, supporting cancellation and progress reporting.

### 8. UUID / GUID Generator (`quickdock.uuid`)
- **Standard Random UUID v4**: Generates cryptographically random GUIDs using `Guid.NewGuid()`.
- **Batch Generation**: Generates 1, 5, 10, or up to 100 UUIDs in a single click.
- **Formatting Styles**: Standard hyphens (`xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`), No hyphens (`xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`), Braces (`{xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx}`), and Uppercase/Lowercase toggles.

### 9. Unix Timestamp Converter (`quickdock.timestamp`)
- **Bidirectional Conversion**:
  - Unix epoch seconds $\rightarrow$ Local and UTC ISO 8601 timestamps.
  - Unix epoch milliseconds $\rightarrow$ Local and UTC ISO 8601 timestamps.
  - Auto-detection heuristic between seconds ($<10^{11}$) and milliseconds ($\ge 10^{11}$).
- **Relative Representation**: Computes humanized relative time (e.g. *"2 hours ago"*, *"in 15 minutes"*).
- **Current Timestamp**: One-click "Now" button to grab the current system epoch timestamp.

### 10. Regex Tester (`quickdock.regex`)
- **Live Match Evaluator**: Evaluates regular expressions against sample text, highlighting match counts, start indices, and lengths.
- **Capturing Groups**: Enumerates named and numbered capturing groups with detailed value inspections.
- **Replacement Preview**: Simulates `Regex.Replace` preview against the input text.
- **ReDoS & Catastrophic Backtracking Protection**: Enforces an explicit 1.5-second execution timeout using `RegexMatchTimeoutException` to safeguard against pathological regex inputs.

### 11. QR Code Generator (`quickdock.qr`)
- **Modes**:
  - Plain text & URL encoding.
  - Wi-Fi network configuration QR format (`WIFI:S:<SSID>;T:<WPA|WEP|nopass>;P:<Password>;;`).
- **High-Resolution Rendering**: Generates clean PNG byte streams using `QRCoder` and renders directly into WinUI 3 via `InMemoryRandomAccessStream`.

### 12. HTTP / API Client (`quickdock.http`)
- **HTTP Methods**: GET, POST, PUT, PATCH, DELETE, HEAD.
- **Headers & Parameters**: Custom multi-line header inputs (`Key: Value`) and query parameters.
- **Payloads**: Request bodies supporting `application/json` and raw text.
- **Response Inspection**:
  - HTTP Status Code and Reason Phrase with colored pill badges.
  - Round-trip elapsed time (ms) and response body size (bytes).
  - Response headers dictionary.
  - Formatted JSON response viewer.
- **Safety**: Utilizes a shared `HttpClient` instance and never logs sensitive credentials, Bearer tokens, or cookies.

### 13. Localhost & Port Monitor (`quickdock.localdev`)
- **Native TCP Inspection**: Uses the native Windows IP Helper API `GetExtendedTcpTable` (`iphlpapi.dll`) to list all active TCP listening sockets without running third-party port scanner processes.
- **Process Correlation**: Matches listening ports to their owning Process ID (PID) and executable name.
- **One-Click Browser Launch**: Click "Open URL" to launch `http://localhost:<PORT>` directly in your default browser.

### 14. Developer Process Monitor (`quickdock.processes`)
- **Process Inventory**: Lists running Windows processes sorted by listening network ports and memory working set (MB).
- **Executable Paths**: Inspects `MainModule.FileName` with graceful fallback when Windows denies access to elevated/system processes.
- **Folder Navigation**: Opens containing folder in Windows Explorer (`explorer.exe /select,"<path>"`).
- **Process Termination**: Safe process termination (`Process.Kill(entireProcessTree: true)`) with system process protection (PIDs $\le 4$ like System and Idle are strictly blocked).

---

## 3. Security & Resource Model

1. **Zero Shell Injection**:
   Git and Docker commands avoid `cmd.exe /c` or shell concatenation. They execute directly via `ProcessStartInfo.ArgumentList`.
2. **Localhost & Local Privacy**:
   All utilities (JSON, JWT, Regex, Base64, Hashing, UUID, Timestamp) execute 100% locally on your CPU. No data is sent to external clouds or telemetry trackers.
3. **Catastrophic Backtracking Guard**:
   Regular expression evaluations enforce strict timeouts to protect the desktop shell against ReDoS attacks.
4. **Process Elevation & System Protection**:
   Process termination checks prevent accidental termination of critical Windows services. Elevated processes are handled gracefully when access is denied.

---

## 4. Verification & Testing

All Phase 7 capabilities are covered by automated unit tests in `QuickDock.Core.Tests`:
- **Git Tests**: Repository discovery, non-git directory handling, branch and commit parsing, argument list safety.
- **Docker Tests**: Engine status queries, container JSON deserialization, daemon connection failure handling.
- **Terminal Tests**: System terminal availability checks.
- **JSON Tests**: Formatting, minification, syntax validation error reporting, large JSON payload stress.
- **JWT Tests**: Base64Url decoding, claim extraction, expired token checks, HMAC-SHA256 signature verification and failure modes.
- **Encoding Tests**: Base64 round-trip, invalid Base64 error handling, URL encoding.
- **Hash Tests**: Verified test vectors for MD5, SHA-1, SHA-256, SHA-384, SHA-512, streaming file hashing with progress reporting.
- **UUID Tests**: Uniqueness across batch generations, format permutations (braces, hyphens, uppercase).
- **Timestamp Tests**: Epoch seconds and milliseconds conversions, relative time descriptions.
- **Regex Tests**: Pattern matching, group captures, replacement previews, ReDoS timeout verification.
- **QR Tests**: PNG generation from text and Wi-Fi payload strings.
- **HTTP Tests**: Request dispatch, timeout cancellation, response JSON formatting.
- **Local Dev & Process Tests**: TCP listening port enumeration via `GetExtendedTcpTable`, process mapping, system process termination block.
- **Lifecycle Tests**: `InitializeAsync`, `StartAsync`, `StopAsync` state transitions across all developer modules.

### Verification Results
```text
Test run for QuickDock.Core.Tests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed: 0, Passed: 139, Skipped: 0, Total: 139, Duration: 5 s
Build: 0 errors, 0 warnings
```

---

## 5. Known Limitations
- Docker features depend on the presence of Docker Desktop / Docker CLI on the host machine. When Docker is not installed or the engine is not running, the module safely indicates an unavailable state rather than simulating mock data.
- Process inspection for protected Windows system processes (e.g. anti-malware, system kernel) will safely display "Access Denied" for executable paths as enforced by Windows security boundaries.

