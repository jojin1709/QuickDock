# QuickDock — Phase 8: Network & Security Toolkit

## 1. Overview & Architecture

Phase 8 introduces a comprehensive, production-grade **Local Windows Network & Defensive Security Diagnostics Toolkit** directly into the QuickDock WinUI 3 desktop shell. It gives developers, system engineers, and security analysts immediate visibility into local network interfaces, listening sockets, active connections, routing, neighbor caches, DNS configurations, TLS certificates, HTTP security headers, firewall profile posture, and local Windows defensive checks.

All network and security modules strictly adhere to the core architectural law:
> **"CORE STAYS ALIVE. MODULES DO NOT."**

### Architectural & Security Guarantees
- **Pure Defensive Diagnostics**: Absolutely **NO** penetration testing, offensive scanning, credential harvesting, brute forcing, mass subnet crawling, exploit generation, or automated compromise tools.
- **On-Demand & Event-Driven**: Zero permanent background network polling or socket monitors. Services are activated solely when requested by the user and go completely dormant when collapsed.
- **Native Windows Interoperability**: Real Win32 IP Helper (`iphlpapi.dll`), native Win32 DNS resolver (`dnsapi.dll`), native .NET sockets, and Windows Security APIs.
- **No Mock or Simulated Data**: Every metric, connection, listening port, certificate detail, and security finding comes from actual local machine operations or explicit user-initiated requests.
- **Zero Secret Disclosure**: Passwords, API tokens, sensitive credentials, and private keys are never queried, captured, or included in diagnostics reports.

---

## 2. Implemented Toolkits & Capabilities

### 1. Network Overview & Interface Diagnostics (`quickdock.networkdiag`)
- **Primary Interface Resolution**: Identifies active network interface, connection type (Ethernet, Wi-Fi, Loopback, Tunnel), and operational status.
- **IP Address Categorization**: Evaluates and categorizes all IPv4 and IPv6 addresses into `Loopback`, `LinkLocal`, `Private`, or `Public`.
- **Gateway & DNS Resolution**: Extracts IPv4 default gateways, DNS server lists, DHCP lease status, and interface link speeds.
- **Interface Enumeration**: Surfaces all physical and virtual network adapters with MAC addresses, connection status, MTU, and descriptions.

### 2. DNS Diagnostics Toolkit (`quickdock.dnsping`)
- **Comprehensive Record Support**: Resolves `A`, `AAAA`, `MX`, `TXT`, `NS`, `CNAME`, and `PTR` records.
- **Native Resolver**: Uses Win32 `DnsQuery_W` in `dnsapi.dll` and .NET asynchronous DNS APIs to query the local DNS resolver cache and configured DNS servers.
- **Syntax Validation**: Validates domain names and IP strings prior to dispatch to prevent injection or invalid requests.
- **TTL & Preference Reporting**: Surfaces record TTLs and MX mail exchange preference priorities.

### 3. Connectivity & Ping Diagnostics
- **Native ICMP Echo**: Dispatches user-configured ping echo requests (default: 4 packets, 32-byte payload, configurable timeout).
- **Latency & Reliability Metrics**: Calculates Min RTT, Max RTT, Average RTT, packets sent, packets received, and packet loss percentage.
- **Gateway Reachability**: One-click default gateway reachability diagnostic.
- **Asynchronous Cancellation**: Immediate abort support via `CancellationTokenSource` without blocking the UI thread.

### 4. Active Connections & Sockets Inspector (`quickdock.netconns`)
- **Native IP Helper Interop**: Uses Win32 `GetExtendedTcpTable` and `GetExtendedUdpTable` in `iphlpapi.dll` with `TCP_TABLE_OWNER_PID_ALL` and `UDP_TABLE_OWNER_PID`.
- **Connection Details**: Protocol (`TCP`, `UDP`), local endpoint (`IP:Port`), remote endpoint (`IP:Port`), and TCP state (`ESTABLISHED`, `LISTENING`, `TIME_WAIT`, `CLOSE_WAIT`, etc.).
- **Process Attribution**: Resolves owning process ID (PID) and process name for every socket.
- **Real-Time Filtering**: Dynamic case-insensitive search by local/remote endpoint, port, protocol, TCP state, or process name.

### 5. Listening Ports Inspector
- **Local Server Detection**: Identifies all active listening sockets across 0.0.0.0, 127.0.0.1, and specific interfaces.
- **Process Mapping**: Maps each listening port to its owning application and PID.

### 6. Routing Table & Neighbor Cache
- **IPv4 Routing Table**: Enumerates routes via `GetIpForwardTable` in `iphlpapi.dll`, showing Destination, Netmask, Gateway, Interface IP, and Metric.
- **Neighbor Cache (ARP)**: Inspects local ARP cache using `GetIpNetTable`, displaying IP-to-MAC mappings and entry states (`Dynamic`, `Static`, `Invalid`).

### 7. TLS / SSL Certificate Inspector (`quickdock.tls`)
- **Isolated Handshake**: Establishes a standalone `TcpClient` connection and negotiates TLS via `SslStream` without altering global system validation.
- **Certificate Chain Parsing**: Inspects X.509 leaf certificate and intermediate certificates.
- **Cryptographic Details**: Surfaces negotiated protocol (`TLS 1.2`, `TLS 1.3`), cipher suite, signature algorithm, serial number, and SHA-1/SHA-256 thumbprints.
- **Subject Alternative Names**: Parses and lists all SANs (`DNS Name`, `IP Address`).
- **Validity & Expiration**: Shows `ValidFrom`, `ValidUntil`, and remaining days with expiration warnings.

### 8. HTTP Security Headers Audit (`quickdock.httpsec`)
- **Defensive Header Evaluation**: Inspects remote web server HTTP responses for essential defensive headers:
  - `Content-Security-Policy` (CSP)
  - `Strict-Transport-Security` (HSTS)
  - `X-Content-Type-Options` (nosniff)
  - `X-Frame-Options` (DENY / SAMEORIGIN)
  - `Referrer-Policy`
  - `Permissions-Policy`
  - `Cross-Origin-Opener-Policy` (COOP)
  - `Cross-Origin-Resource-Policy` (CORP)
  - `Cache-Control`
- **Information Disclosure Assessment**: Evaluates the `Server` header and warns if detailed software/version information is leaked.

### 9. Public IP & Domain Inspector
- **Safe Public IP Query**: User-initiated external IP resolution via trusted HTTPS endpoint (`api.ipify.org`).
- **Reverse DNS**: PTR record lookup for arbitrary hostnames and IP addresses.

### 10. Windows Firewall Status (`quickdock.firewall`)
- **Profile State**: Inspects state of Domain, Private, and Public profiles.
- **Inbound/Outbound Policies**: Displays default inbound (Block) and outbound (Allow) action policies.
- **Resilient Fallback**: Handles stopped services and restricted execution environments gracefully with clear error reporting.

### 11. Microsoft Defender & Antivirus Status
- **Service Verification**: Checks `WinDefend` service state via native Win32 `sc.exe query`.
- **Real-Time Protection**: Checks real-time protection enabled state via `Get-MpComputerStatus`.
- **Product Information**: Displays active antivirus product name and protection status.

### 12. Local Security Posture Checks (`quickdock.posture`)
- **Account Elevation**: Checks whether QuickDock is running in an elevated Administrator security context.
- **User Account Control (UAC)**: Queries registry (`EnableLUA`) to confirm UAC enforcement is active.
- **Secure Boot**: Queries UEFI environment variables to confirm Secure Boot state.
- **Development Port Exposure**: Flags dangerous binding of common developer ports (3000, 5000, 5173, 8000, 8080, 9229, etc.) on `0.0.0.0` (all interfaces) rather than `127.0.0.1` (localhost only).

### 13. File Integrity Security Tools (`quickdock.integrity`)
- **Cryptographic Checksums**: Computes SHA-256, SHA-384, or SHA-512 hashes for local files.
- **Streaming Execution**: Non-blocking streaming with progress reporting for multi-gigabyte files.
- **Checksum Verification**: Compares computed hash against user-supplied expected hash (case-insensitive) with matching confirmation.

### 14. Defensive Security Report Generator (`quickdock.secreport`)
- **Comprehensive Compilation**: Aggregates network overview, active adapters, listening ports, active sockets, firewall posture, Defender state, and local security findings.
- **Formats**: Generates human-readable plaintext (`.txt`) and structured JSON (`.json`).
- **Legal & Safety Disclaimers**: Prominently includes defensive warnings and cautions regarding sharing sensitive network topology.
- **One-Click Export**: Copy to clipboard with instant confirmation.

---

## 3. Module Matrix

| Module ID | Module Title | Category | Initial State | Primary Function |
|---|---|---|---|---|
| `quickdock.networkdiag` | Network Diagnostics | Utilities | Uninitialized | Network overview, interfaces, routing, neighbor cache |
| `quickdock.securitydiag` | Security Diagnostics | Utilities | Uninitialized | Local security posture, UAC, Secure Boot, elevation audit |
| `quickdock.tls` | TLS Inspector | Utilities | Uninitialized | Remote certificate inspection, ciphers, SANs, chain |
| `quickdock.httpsecurity` | Security Headers | Utilities | Uninitialized | Defensive HTTP security headers evaluation |
| `quickdock.firewall` | Firewall & Defender | Utilities | Uninitialized | Native Windows Firewall & Defender real-time state |

---

## 4. Verification & Testing

- **Automated Tests**: 166 passed, 0 failed, 0 skipped.
- **Build Status**: Solution builds cleanly with 0 errors and 0 warnings.
- **Runtime Binary Verification**: `QuickDock.App.exe` launched, executed WinUI 3 shell, and terminated cleanly.
