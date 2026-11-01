# Security Policy

## Supported Versions

QuickDock is actively maintained and hardened against security vulnerabilities. Only the latest official release receives active security patches.

| Version | Supported | Status |
| :--- | :--- | :--- |
| **1.0.x** | Yes | Current Production Release |
| **< 1.0.0** | No | Deprecated Pre-release Builds |

---

## Reporting a Vulnerability

If you discover a security vulnerability, flaw, or potential exploit in QuickDock, please report it responsibly.

### Disclosure Channels
- **Author:** JOJIN JOHN (Software Engineer & Security Researcher)
- **LinkedIn:** [https://www.linkedin.com/in/jojin-john/](https://www.linkedin.com/in/jojin-john/)
- **GitHub Private Vulnerability Reporting:** If enabled on this repository, you may submit a confidential report directly via the GitHub Security Advisory tab.

Please include the following details in your report:
1. A clear description of the vulnerability and its potential impact.
2. Steps to reproduce the issue, including proof-of-concept commands or test files.
3. Your operating system build and architecture (e.g. Windows 11 Build 26100 x64).
4. Any relevant logs, screenshots, or stack traces.

### Response Commitment
- **Initial Acknowledgment:** Within 48 hours of receipt.
- **Vulnerability Assessment:** Within 5 business days.
- **Resolution & Patch:** Coordinated release with priority deployment.

Please do **not** disclose vulnerabilities publicly in open GitHub issues until a coordinated patch has been published.

---

## Security Architecture & Design Guarantees

QuickDock is engineered from the ground up under a strict **Zero-Telemetry, Local-First, Defensive** model:

1. **Zero Cloud & Zero Telemetry:**
   - QuickDock never transmits user data, clipboard contents, notes, hardware metrics, or telemetry to external servers.
   - All persistence is maintained locally on your machine (`%LOCALAPPDATA%\QuickDock\quickdock.db`).

2. **Timing-Safe Authentication:**
   - QuickDrop local device transfers use `CryptographicOperations.FixedTimeEquals` to eliminate timing side-channel attacks during session token verification.

3. **Path Traversal & DOS Device Mitigation:**
   - Filename sanitization scrubs directory escape sequences (`../`) and neutralizes DOS reserved device names (`CON`, `PRN`, `AUX`, `NUL`, `COM1..9`, `LPT1..9`).

4. **Zip Bomb & Archive Safety:**
   - Archive extraction pipelines enforce strict byte limits (maximum 2 GB decompressed) and entry count limits (maximum 10,000 files) to prevent denial-of-service via disk exhaustion.

5. **Shell Command Injection Defense:**
   - Terminal and Git CLI executions use structured argument lists (`ProcessStartInfo.ArgumentList`) rather than shell string concatenation, neutralizing metacharacter injection.

6. **Process Termination Protection:**
   - Process management utilities strictly protect core system PIDs (PID <= 4) and the QuickDock host process itself from accidental or malicious termination.

7. **Cryptographic Integrity:**
   - All official release packages include published SHA-256 checksums (`SHA256SUMS.txt`) to ensure binary verification prior to execution.
