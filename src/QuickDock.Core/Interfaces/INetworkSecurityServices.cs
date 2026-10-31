using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QuickDock.Core.Models;

namespace QuickDock.Core.Interfaces;

public interface INetworkOverviewService
{
    Task<NetworkOverviewInfo> GetNetworkOverviewAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NetworkAdapterDetail>> GetAdaptersAsync(CancellationToken cancellationToken = default);
}

public interface IDnsDiagnosticService
{
    Task<DnsQueryResult> QueryDnsAsync(string domain, DnsRecordType recordType, CancellationToken cancellationToken = default);
}

public interface IConnectivityService
{
    Task<ConnectivityDiagnosticsResult> PingAsync(string hostOrIp, int count = 4, int timeoutMs = 2000, CancellationToken cancellationToken = default);
    Task<bool> TestGatewayConnectivityAsync(CancellationToken cancellationToken = default);
}

public interface INetworkConnectionService
{
    Task<IReadOnlyList<ActiveConnectionInfo>> GetActiveConnectionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ListeningPortInfo>> GetListeningSocketsAsync(CancellationToken cancellationToken = default);
}

public interface IRoutingNeighborService
{
    Task<IReadOnlyList<NetworkRouteInfo>> GetRoutingTableAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NeighborCacheInfo>> GetNeighborCacheAsync(CancellationToken cancellationToken = default);
}

public interface ITlsInspectionService
{
    Task<TlsInspectionResult> InspectTlsAsync(string host, int port = 443, int timeoutSeconds = 10, CancellationToken cancellationToken = default);
}

public interface IHttpSecurityService
{
    Task<HttpSecurityHeadersResult> InspectHeadersAsync(string url, int timeoutSeconds = 10, CancellationToken cancellationToken = default);
}

public interface IIpDomainService
{
    Task<IpDomainInfo> ResolveIpDomainAsync(string hostOrIp, CancellationToken cancellationToken = default);
    Task<PublicIpLookupResult> LookupPublicIpAsync(string provider = "api.ipify.org", CancellationToken cancellationToken = default);
}

public interface IFirewallStatusService
{
    Task<FirewallStatusInfo> GetFirewallStatusAsync(CancellationToken cancellationToken = default);
}

public interface IWindowsSecurityService
{
    Task<WindowsSecurityStatusInfo> GetWindowsSecurityStatusAsync(CancellationToken cancellationToken = default);
}

public interface ILocalSecurityCheckService
{
    Task<LocalSecurityCheckResult> RunLocalSecurityChecksAsync(CancellationToken cancellationToken = default);
}

public interface IFileIntegrityService
{
    Task<FileIntegrityComparisonResult> VerifyFileIntegrityAsync(
        string filePath,
        DeveloperHashAlgorithm algorithm,
        string? expectedHash = null,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface ISecurityReportService
{
    Task<SecurityReportResult> GenerateReportAsync(SecurityReportRequest request, CancellationToken cancellationToken = default);
}

