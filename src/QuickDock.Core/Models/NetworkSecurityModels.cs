using System;
using System.Collections.Generic;

namespace QuickDock.Core.Models;

#region Network Overview & Adapters

public enum IpAddressCategory
{
    Loopback,
    LinkLocal,
    Private,
    Public
}

public sealed class IpAddressDetail
{
    public required string Address { get; init; }
    public string Family { get; init; } = "IPv4"; // IPv4 or IPv6
    public IpAddressCategory Category { get; init; } = IpAddressCategory.Private;
    public string Description => $"{Address} ({Family}, {Category})";
}

public sealed class NetworkOverviewInfo
{
    public string Hostname { get; init; } = string.Empty;
    public string ComputerName { get; init; } = string.Empty;
    public string PrimaryInterfaceName { get; init; } = string.Empty;
    public string ConnectionType { get; init; } = "Unknown";
    public string OperationalStatus { get; init; } = "Unknown";
    public string PrimaryIpv4 { get; init; } = string.Empty;
    public string PrimaryIpv6 { get; init; } = string.Empty;
    public string DefaultGateway { get; init; } = string.Empty;
    public List<string> DnsServers { get; init; } = [];
    public bool IsDhcpEnabled { get; init; }
    public string MacAddress { get; init; } = string.Empty;
    public string LinkSpeed { get; init; } = string.Empty;
}

public sealed class NetworkAdapterDetail
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = "Down";
    public string InterfaceType { get; init; } = "Ethernet";
    public string MacAddress { get; init; } = string.Empty;
    public List<IpAddressDetail> IpAddresses { get; init; } = [];
    public List<string> Gateways { get; init; } = [];
    public List<string> DnsServers { get; init; } = [];
    public bool IsDhcpEnabled { get; init; }
    public string SpeedFormatted { get; init; } = string.Empty;
    public bool IsUp => string.Equals(Status, "Up", StringComparison.OrdinalIgnoreCase);
}

#endregion

#region DNS Models

public enum DnsRecordType
{
    A = 1,
    NS = 2,
    CNAME = 5,
    PTR = 12,
    MX = 15,
    TXT = 16,
    AAAA = 28
}

public sealed class DnsRecordItem
{
    public required string Name { get; init; }
    public DnsRecordType RecordType { get; init; }
    public required string Value { get; init; }
    public int Ttl { get; init; }
    public int Preference { get; init; } // For MX
}

public sealed class DnsQueryResult
{
    public bool Success { get; init; }
    public string Domain { get; init; } = string.Empty;
    public DnsRecordType RecordType { get; init; }
    public List<DnsRecordItem> Records { get; init; } = [];
    public long ElapsedMilliseconds { get; init; }
    public string ResolverUsed { get; init; } = "System DNS";
    public string ErrorMessage { get; init; } = string.Empty;
}

#endregion

#region Connectivity Models

public sealed class PingEchoResult
{
    public int Sequence { get; init; }
    public bool Success { get; init; }
    public string IpAddress { get; init; } = string.Empty;
    public long RoundTripTimeMs { get; init; }
    public int Ttl { get; init; }
    public string Status { get; init; } = "Unknown";
}

public sealed class ConnectivityDiagnosticsResult
{
    public string TargetHost { get; init; } = string.Empty;
    public string ResolvedIp { get; init; } = string.Empty;
    public bool IsReachable { get; init; }
    public List<PingEchoResult> Echoes { get; init; } = [];
    public double AverageRttMs { get; init; }
    public int PacketsSent { get; init; }
    public int PacketsReceived { get; init; }
    public double PacketLossPercentage => PacketsSent > 0
        ? Math.Round(((PacketsSent - PacketsReceived) / (double)PacketsSent) * 100.0, 1)
        : 0;
    public string SummaryText { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

#endregion

#region Local Connections & Routing

public enum ConnectionProtocol
{
    TCP,
    UDP
}

public sealed class ActiveConnectionInfo
{
    public ConnectionProtocol Protocol { get; init; } = ConnectionProtocol.TCP;
    public string LocalAddress { get; init; } = "127.0.0.1";
    public int LocalPort { get; init; }
    public string RemoteAddress { get; init; } = "0.0.0.0";
    public int RemotePort { get; init; }
    public string State { get; init; } = "ESTABLISHED";
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = "System";
    public string LocalEndpoint => $"{LocalAddress}:{LocalPort}";
    public string RemoteEndpoint => RemotePort > 0 ? $"{RemoteAddress}:{RemotePort}" : "*:*";
}

public sealed class NetworkRouteInfo
{
    public string Destination { get; init; } = string.Empty;
    public string Netmask { get; init; } = string.Empty;
    public string Gateway { get; init; } = string.Empty;
    public string InterfaceAddress { get; init; } = string.Empty;
    public int Metric { get; init; }
}

public sealed class NeighborCacheInfo
{
    public string IpAddress { get; init; } = string.Empty;
    public string MacAddress { get; init; } = string.Empty;
    public string State { get; init; } = "Dynamic"; // Dynamic, Static, Invalid
    public int InterfaceIndex { get; init; }
    public string InterfaceName { get; init; } = string.Empty;
}

#endregion

#region TLS & HTTP Security

public sealed class TlsChainElement
{
    public required string Subject { get; init; }
    public required string Issuer { get; init; }
    public string Thumbprint { get; init; } = string.Empty;
}

public sealed class TlsInspectionResult
{
    public bool Success { get; init; }
    public string TargetHost { get; init; } = string.Empty;
    public int TargetPort { get; init; } = 443;
    public string NegotiatedProtocol { get; init; } = "None"; // TLS 1.2, TLS 1.3
    public string CipherSuite { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string CommonName { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public DateTime ValidFrom { get; init; }
    public DateTime ValidUntil { get; init; }
    public bool IsExpired => DateTime.UtcNow > ValidUntil;
    public int DaysRemaining => (int)Math.Max(0, (ValidUntil - DateTime.UtcNow).TotalDays);
    public string Thumbprint { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public string SignatureAlgorithm { get; init; } = string.Empty;
    public List<string> SubjectAlternativeNames { get; init; } = [];
    public List<TlsChainElement> CertificateChain { get; init; } = [];
    public bool IsValidationSuccessful { get; init; }
    public string ValidationStatusMessage { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed class HeaderCheckItem
{
    public required string HeaderName { get; init; }
    public bool IsPresent { get; init; }
    public string Value { get; init; } = string.Empty;
    public string Assessment { get; init; } = "Header not present";
    public string Recommendation { get; init; } = string.Empty;
}

public sealed class HttpSecurityHeadersResult
{
    public bool Success { get; init; }
    public string TargetUrl { get; init; } = string.Empty;
    public int StatusCode { get; init; }
    public string StatusText { get; init; } = string.Empty;
    public long ResponseTimeMs { get; init; }
    public List<HeaderCheckItem> HeaderChecks { get; init; } = [];
    public Dictionary<string, string> RawHeaders { get; init; } = [];
    public string ErrorMessage { get; init; } = string.Empty;
}

#endregion

#region IP / Domain & Public IP

public sealed class IpDomainInfo
{
    public string InputQuery { get; init; } = string.Empty;
    public string NormalizedDomain { get; init; } = string.Empty;
    public List<string> Ipv4Addresses { get; init; } = [];
    public List<string> Ipv6Addresses { get; init; } = [];
    public string ReverseDns { get; init; } = string.Empty;
    public bool IsResolvable => Ipv4Addresses.Count > 0 || Ipv6Addresses.Count > 0;
}

public sealed class PublicIpLookupResult
{
    public bool Success { get; init; }
    public string PublicIp { get; init; } = string.Empty;
    public string Provider { get; init; } = "api.ipify.org";
    public long ElapsedMilliseconds { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
}

#endregion

#region Firewall & Windows Security

public sealed class FirewallProfileStatus
{
    public required string ProfileName { get; init; } // Domain, Private, Public
    public bool IsEnabled { get; init; }
    public string InboundAction { get; init; } = "Block";
    public string OutboundAction { get; init; } = "Allow";
}

public sealed class FirewallStatusInfo
{
    public bool IsAccessible { get; init; } = true;
    public List<FirewallProfileStatus> Profiles { get; init; } = [];
    public string SummaryText { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
}

public sealed class WindowsSecurityStatusInfo
{
    public bool IsDefenderInstalled { get; init; }
    public bool IsRealTimeProtectionEnabled { get; init; }
    public bool IsAntivirusActive { get; init; }
    public string AntivirusProductName { get; init; } = "Windows Defender";
    public string SecurityCenterStatus { get; init; } = "Normal";
    public string StatusDetails { get; init; } = string.Empty;
    public bool HasAccessRestrictions { get; init; }
}

#endregion

#region Local Security Checks & Findings

public enum FindingSeverity
{
    Informational,
    Low,
    Medium,
    High
}

public sealed class SecurityFinding
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Category { get; init; } // Account, Network, System, Service
    public FindingSeverity Severity { get; init; } = FindingSeverity.Informational;
    public required string Description { get; init; }
    public string Recommendation { get; init; } = string.Empty;
}

public sealed class LocalSecurityCheckResult
{
    public DateTime ScanTimestamp { get; init; } = DateTime.UtcNow;
    public string CurrentUser { get; init; } = string.Empty;
    public bool IsAdministrator { get; init; }
    public bool IsUacEnabled { get; init; }
    public bool IsSecureBootEnabled { get; init; }
    public List<SecurityFinding> Findings { get; init; } = [];
    public int HighCount => Findings.FindAll(f => f.Severity == FindingSeverity.High).Count;
    public int MediumCount => Findings.FindAll(f => f.Severity == FindingSeverity.Medium).Count;
    public int LowCount => Findings.FindAll(f => f.Severity == FindingSeverity.Low).Count;
    public int InfoCount => Findings.FindAll(f => f.Severity == FindingSeverity.Informational).Count;
}

#endregion

#region File Integrity Security

public sealed class FileIntegrityComparisonResult
{
    public required string FilePath { get; init; }
    public string FileName => System.IO.Path.GetFileName(FilePath);
    public long FileSizeBytes { get; init; }
    public DeveloperHashAlgorithm Algorithm { get; init; } = DeveloperHashAlgorithm.SHA256;
    public required string ComputedHash { get; init; }
    public string ExpectedHash { get; init; } = string.Empty;
    public bool HasExpectedHash => !string.IsNullOrWhiteSpace(ExpectedHash);
    public bool IsMatch => HasExpectedHash && string.Equals(ComputedHash.Trim(), ExpectedHash.Trim(), StringComparison.OrdinalIgnoreCase);
    public long ElapsedMilliseconds { get; init; }
}

#endregion

#region Security Reports

public enum SecurityReportFormat
{
    Text,
    Json
}

public sealed class SecurityReportRequest
{
    public SecurityReportFormat Format { get; init; } = SecurityReportFormat.Text;
    public bool IncludeNetworkAdapters { get; init; } = true;
    public bool IncludeListeningPorts { get; init; } = true;
    public bool IncludeActiveConnections { get; init; } = true;
    public bool IncludeFirewallStatus { get; init; } = true;
    public bool IncludeSecurityChecks { get; init; } = true;
}

public sealed class SecurityReportResult
{
    public bool Success { get; init; }
    public SecurityReportFormat Format { get; init; }
    public string ReportContent { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string Disclaimer { get; init; } = "This diagnostic report contains sensitive local network and system configuration details. Handle securely.";
    public string ErrorMessage { get; init; } = string.Empty;
}

#endregion

