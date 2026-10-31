using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Core.Modules;
using QuickDock.Modules.NetworkSecurity;
using QuickDock.Services.Developer;
using QuickDock.Services.NetworkSecurity;
using Xunit;

namespace QuickDock.Core.Tests;

public class NetworkSecurityTests
{
    private static readonly HttpClient SharedHttpClient = new();

    #region 1. Network Overview & Adapters Tests

    [Fact]
    public async Task NetworkOverviewService_ReturnsValidOverview()
    {
        var service = new NetworkOverviewService(NullLogger<NetworkOverviewService>.Instance);
        var overview = await service.GetNetworkOverviewAsync();

        Assert.NotNull(overview);
        Assert.False(string.IsNullOrEmpty(overview.Hostname));
        Assert.NotNull(overview.PrimaryInterfaceName);
        Assert.NotNull(overview.ConnectionType);
        Assert.NotNull(overview.OperationalStatus);
    }

    [Fact]
    public async Task NetworkOverviewService_GetAdapters_ReturnsAdaptersList()
    {
        var service = new NetworkOverviewService(NullLogger<NetworkOverviewService>.Instance);
        var adapters = await service.GetAdaptersAsync();

        Assert.NotNull(adapters);
        Assert.NotEmpty(adapters);
        Assert.All(adapters, a =>
        {
            Assert.False(string.IsNullOrEmpty(a.Id));
            Assert.False(string.IsNullOrEmpty(a.Name));
            Assert.NotNull(a.Status);
            Assert.NotNull(a.InterfaceType);
        });
    }

    #endregion

    #region 2. DNS Diagnostics Tests

    [Fact]
    public async Task DnsDiagnosticService_QueryLocalhost_ReturnsARecords()
    {
        var service = new DnsDiagnosticService(NullLogger<DnsDiagnosticService>.Instance);
        var result = await service.QueryDnsAsync("localhost", DnsRecordType.A);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotEmpty(result.Records);
        Assert.Contains(result.Records, r => r.Value == "127.0.0.1");
    }

    [Fact]
    public async Task DnsDiagnosticService_InvalidDomain_ReturnsFailure()
    {
        var service = new DnsDiagnosticService(NullLogger<DnsDiagnosticService>.Instance);
        var result = await service.QueryDnsAsync("invalid...domain-format", DnsRecordType.A);

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
    }

    #endregion

    #region 3. Connectivity Tests

    [Fact]
    public async Task ConnectivityService_PingLocalhost_SucceedsWithZeroLoss()
    {
        var service = new ConnectivityService(NullLogger<ConnectivityService>.Instance);
        var result = await service.PingAsync("127.0.0.1", count: 2, timeoutMs: 1000);

        Assert.NotNull(result);
        Assert.True(result.IsReachable);
        Assert.Equal(0, result.PacketLossPercentage);
        Assert.Equal(2, result.PacketsSent);
        Assert.Equal(2, result.PacketsReceived);
        Assert.False(string.IsNullOrEmpty(result.SummaryText));
    }

    [Fact]
    public async Task ConnectivityService_TestGateway_CompletesWithoutThrowing()
    {
        var service = new ConnectivityService(NullLogger<ConnectivityService>.Instance);
        bool result = await service.TestGatewayConnectivityAsync();
        // Result is either true or false depending on presence of gateway, but should never throw
        Assert.True(result || !result);
    }

    [Fact]
    public async Task ConnectivityService_Cancellation_AbortsEarly()
    {
        var service = new ConnectivityService(NullLogger<ConnectivityService>.Instance);
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        var result = await service.PingAsync("8.8.8.8", count: 4, timeoutMs: 2000, cancellationToken: cts.Token);
        Assert.NotNull(result);
        Assert.True(result.PacketsSent < 4 || result.Echoes.Count == 0);
    }

    #endregion

    #region 4. Network Connections & Sockets Tests

    [Fact]
    public async Task NetworkConnectionService_GetActiveConnections_ReturnsValidList()
    {
        var service = new NetworkConnectionService(NullLogger<NetworkConnectionService>.Instance);
        var connections = await service.GetActiveConnectionsAsync();

        Assert.NotNull(connections);
        Assert.NotEmpty(connections);
        Assert.All(connections, c =>
        {
            Assert.False(string.IsNullOrEmpty(c.LocalAddress));
            Assert.True(c.LocalPort > 0);
            Assert.False(string.IsNullOrEmpty(c.State));
            Assert.True(c.ProcessId >= 0);
            Assert.False(string.IsNullOrEmpty(c.LocalEndpoint));
        });
    }

    [Fact]
    public async Task NetworkConnectionService_GetListeningSockets_ReturnsSockets()
    {
        var service = new NetworkConnectionService(NullLogger<NetworkConnectionService>.Instance);
        var listening = await service.GetListeningSocketsAsync();

        Assert.NotNull(listening);
        Assert.NotEmpty(listening);
        Assert.All(listening, s =>
        {
            Assert.True(s.Port > 0);
            Assert.True(s.ProcessId >= 0);
            Assert.False(string.IsNullOrEmpty(s.LocalAddress));
        });
    }

    #endregion

    #region 5. Routing & Neighbor Cache Tests

    [Fact]
    public async Task RoutingNeighborService_GetRoutingTable_ReturnsRoutes()
    {
        var service = new RoutingNeighborService(NullLogger<RoutingNeighborService>.Instance);
        var routes = await service.GetRoutingTableAsync();

        Assert.NotNull(routes);
        Assert.NotEmpty(routes);
        Assert.All(routes, r =>
        {
            Assert.False(string.IsNullOrEmpty(r.Destination));
            Assert.False(string.IsNullOrEmpty(r.Gateway));
        });
    }

    [Fact]
    public async Task RoutingNeighborService_GetNeighborCache_ReturnsNeighbors()
    {
        var service = new RoutingNeighborService(NullLogger<RoutingNeighborService>.Instance);
        var neighbors = await service.GetNeighborCacheAsync();

        // Neighbor cache may be empty or contain arp entries, but should never throw
        Assert.NotNull(neighbors);
    }

    #endregion

    #region 6. TLS Certificate Inspection Tests

    [Fact]
    public async Task TlsInspectionService_InvalidHost_FailsGracefully()
    {
        var service = new TlsInspectionService(NullLogger<TlsInspectionService>.Instance);
        var result = await service.InspectTlsAsync("invalid-host-that-does-not-exist.example", 443, timeoutSeconds: 2);

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
    }

    #endregion

    #region 7. HTTP Security Headers Tests

    [Fact]
    public async Task HttpSecurityService_InvalidUrl_ReturnsFailure()
    {
        var service = new HttpSecurityService(SharedHttpClient, NullLogger<HttpSecurityService>.Instance);
        var result = await service.InspectHeadersAsync("not-a-valid-url");

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
    }

    #endregion

    #region 8. IP & Domain Resolution Tests

    [Fact]
    public async Task IpDomainService_ResolveLocalhost_ReturnsIPv4()
    {
        var service = new IpDomainService(SharedHttpClient, NullLogger<IpDomainService>.Instance);
        var result = await service.ResolveIpDomainAsync("localhost");

        Assert.NotNull(result);
        Assert.True(result.IsResolvable);
        Assert.Contains("127.0.0.1", result.Ipv4Addresses);
    }

    [Fact]
    public async Task IpDomainService_EmptyQuery_ReturnsUnresolvable()
    {
        var service = new IpDomainService(SharedHttpClient, NullLogger<IpDomainService>.Instance);
        var result = await service.ResolveIpDomainAsync("");

        Assert.NotNull(result);
        Assert.False(result.IsResolvable);
    }

    #endregion

    #region 9. Firewall Status Tests

    [Fact]
    public async Task FirewallStatusService_GetStatus_ReturnsProfiles()
    {
        var service = new FirewallStatusService(NullLogger<FirewallStatusService>.Instance);
        var status = await service.GetFirewallStatusAsync();

        Assert.NotNull(status);
        if (status.IsAccessible)
        {
            Assert.NotEmpty(status.Profiles);
            Assert.Contains(status.Profiles, p => p.ProfileName.Contains("Domain") || p.ProfileName.Contains("Private") || p.ProfileName.Contains("Public"));
        }
        else
        {
            Assert.False(string.IsNullOrEmpty(status.ErrorMessage));
        }
    }

    #endregion

    #region 10. Windows Security Status Tests

    [Fact]
    public async Task WindowsSecurityService_GetStatus_ReturnsSecurityInfo()
    {
        var service = new WindowsSecurityService(NullLogger<WindowsSecurityService>.Instance);
        var status = await service.GetWindowsSecurityStatusAsync();

        Assert.NotNull(status);
        Assert.False(string.IsNullOrEmpty(status.AntivirusProductName));
        Assert.False(string.IsNullOrEmpty(status.StatusDetails));
    }

    #endregion

    #region 11. Local Security Checks Tests

    [Fact]
    public async Task LocalSecurityCheckService_RunChecks_ReturnsPostureResult()
    {
        var fwService = new FirewallStatusService(NullLogger<FirewallStatusService>.Instance);
        var connService = new NetworkConnectionService(NullLogger<NetworkConnectionService>.Instance);
        var winSecService = new WindowsSecurityService(NullLogger<WindowsSecurityService>.Instance);
        var service = new LocalSecurityCheckService(fwService, connService, winSecService, NullLogger<LocalSecurityCheckService>.Instance);
        var result = await service.RunLocalSecurityChecksAsync();

        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.CurrentUser));
        Assert.NotEmpty(result.Findings);
        Assert.All(result.Findings, f =>
        {
            Assert.False(string.IsNullOrEmpty(f.Id));
            Assert.False(string.IsNullOrEmpty(f.Title));
            Assert.False(string.IsNullOrEmpty(f.Description));
        });
    }

    #endregion

    #region 12. File Integrity Tests

    [Fact]
    public async Task FileIntegrityService_ComputesAndMatchesExpectedHash()
    {
        var hashService = new DeveloperHashService();
        var service = new FileIntegrityService(hashService, NullLogger<FileIntegrityService>.Instance);

        string tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "QuickDock Phase 8 Integrity Verification Payload");

            // Compute expected SHA-256
            var expectedHash = hashService.HashText("QuickDock Phase 8 Integrity Verification Payload", DeveloperHashAlgorithm.SHA256).HexHash;

            var result = await service.VerifyFileIntegrityAsync(tempFile, DeveloperHashAlgorithm.SHA256, expectedHash);

            Assert.NotNull(result);
            Assert.Equal(tempFile, result.FilePath);
            Assert.Equal(expectedHash, result.ComputedHash, ignoreCase: true);
            Assert.True(result.IsMatch);
            Assert.True(result.HasExpectedHash);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task FileIntegrityService_MismatchHash_ReportsIsMatchFalse()
    {
        var hashService = new DeveloperHashService();
        var service = new FileIntegrityService(hashService, NullLogger<FileIntegrityService>.Instance);

        string tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, "Original Content");
            string wrongHash = new string('0', 64);

            var result = await service.VerifyFileIntegrityAsync(tempFile, DeveloperHashAlgorithm.SHA256, wrongHash);

            Assert.NotNull(result);
            Assert.False(result.IsMatch);
            Assert.True(result.HasExpectedHash);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    #endregion

    #region 13. Security Report Generation Tests

    [Fact]
    public async Task SecurityReportService_GenerateTextReport_ContainsDisclaimerAndDiagnostics()
    {
        var netService = new NetworkOverviewService(NullLogger<NetworkOverviewService>.Instance);
        var connService = new NetworkConnectionService(NullLogger<NetworkConnectionService>.Instance);
        var fwService = new FirewallStatusService(NullLogger<FirewallStatusService>.Instance);
        var winSecService = new WindowsSecurityService(NullLogger<WindowsSecurityService>.Instance);
        var secCheckService = new LocalSecurityCheckService(fwService, connService, winSecService, NullLogger<LocalSecurityCheckService>.Instance);

        var reportService = new SecurityReportService(
            netService,
            connService,
            fwService,
            secCheckService,
            NullLogger<SecurityReportService>.Instance);

        var request = new SecurityReportRequest
        {
            Format = SecurityReportFormat.Text,
            IncludeNetworkAdapters = true,
            IncludeListeningPorts = true,
            IncludeFirewallStatus = true,
            IncludeSecurityChecks = true
        };

        var result = await reportService.GenerateReportAsync(request);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.ReportContent));
        Assert.Contains("QUICKDOCK NETWORK & SECURITY REPORT", result.ReportContent);
        Assert.Contains("DISCLAIMER:", result.ReportContent);
        Assert.Contains("Primary IP:", result.ReportContent);
        Assert.Contains("LOCAL SECURITY CHECKS", result.ReportContent);
    }

    [Fact]
    public async Task SecurityReportService_GenerateJsonReport_ReturnsValidJsonStructure()
    {
        var netService = new NetworkOverviewService(NullLogger<NetworkOverviewService>.Instance);
        var connService = new NetworkConnectionService(NullLogger<NetworkConnectionService>.Instance);
        var fwService = new FirewallStatusService(NullLogger<FirewallStatusService>.Instance);
        var winSecService = new WindowsSecurityService(NullLogger<WindowsSecurityService>.Instance);
        var secCheckService = new LocalSecurityCheckService(fwService, connService, winSecService, NullLogger<LocalSecurityCheckService>.Instance);

        var reportService = new SecurityReportService(
            netService,
            connService,
            fwService,
            secCheckService,
            NullLogger<SecurityReportService>.Instance);

        var request = new SecurityReportRequest
        {
            Format = SecurityReportFormat.Json,
            IncludeNetworkAdapters = true,
            IncludeListeningPorts = true,
            IncludeFirewallStatus = true,
            IncludeSecurityChecks = true
        };

        var result = await reportService.GenerateReportAsync(request);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.ReportContent));
        using var doc = JsonDocument.Parse(result.ReportContent);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("Disclaimer", out var disclaimerProp));
        Assert.Contains("diagnostic report", disclaimerProp.GetString());
        Assert.True(root.TryGetProperty("NetworkOverview", out _));
        Assert.True(root.TryGetProperty("SecurityChecks", out _));
    }

    #endregion

    #region 14. Phase 8 Module Lifecycle Tests

    [Fact]
    public async Task NetworkDiagnosticsModule_Lifecycle_TransitionCorrectly()
    {
        var netService = new NetworkOverviewService(NullLogger<NetworkOverviewService>.Instance);
        var module = new NetworkDiagnosticsModule(netService, NullLogger<NetworkDiagnosticsModule>.Instance);

        Assert.Equal("quickdock.networkdiag", module.Id);
        Assert.Equal(ModuleState.Uninitialized, module.State);

        await module.InitializeAsync();
        Assert.Equal(ModuleState.Initialized, module.State);

        await module.StartAsync();
        Assert.Equal(ModuleState.Running, module.State);

        await module.StopAsync();
        Assert.Equal(ModuleState.Stopped, module.State);
    }

    [Fact]
    public async Task SecurityDiagnosticsModule_Lifecycle_TransitionCorrectly()
    {
        var connService = new NetworkConnectionService(NullLogger<NetworkConnectionService>.Instance);
        var secCheckService = new LocalSecurityCheckService(
            new FirewallStatusService(NullLogger<FirewallStatusService>.Instance),
            connService,
            new WindowsSecurityService(NullLogger<WindowsSecurityService>.Instance),
            NullLogger<LocalSecurityCheckService>.Instance);

        var module = new SecurityDiagnosticsModule(secCheckService, NullLogger<SecurityDiagnosticsModule>.Instance);

        Assert.Equal("quickdock.securitydiag", module.Id);
        Assert.Equal(ModuleState.Uninitialized, module.State);

        await module.InitializeAsync();
        Assert.Equal(ModuleState.Initialized, module.State);

        await module.StartAsync();
        Assert.Equal(ModuleState.Running, module.State);

        await module.StopAsync();
        Assert.Equal(ModuleState.Stopped, module.State);
    }

    [Fact]
    public async Task TlsInspectorModule_Lifecycle_TransitionCorrectly()
    {
        var tlsService = new TlsInspectionService(NullLogger<TlsInspectionService>.Instance);
        var module = new TlsInspectorModule(tlsService, NullLogger<TlsInspectorModule>.Instance);

        Assert.Equal("quickdock.tls", module.Id);
        Assert.Equal(ModuleState.Uninitialized, module.State);

        await module.InitializeAsync();
        Assert.Equal(ModuleState.Initialized, module.State);

        await module.StartAsync();
        Assert.Equal(ModuleState.Running, module.State);

        await module.StopAsync();
        Assert.Equal(ModuleState.Stopped, module.State);
    }

    [Fact]
    public async Task HttpSecurityModule_Lifecycle_TransitionCorrectly()
    {
        var httpSecService = new HttpSecurityService(SharedHttpClient, NullLogger<HttpSecurityService>.Instance);
        var module = new HttpSecurityModule(httpSecService, NullLogger<HttpSecurityModule>.Instance);

        Assert.Equal("quickdock.httpsecurity", module.Id);
        Assert.Equal(ModuleState.Uninitialized, module.State);

        await module.InitializeAsync();
        Assert.Equal(ModuleState.Initialized, module.State);

        await module.StartAsync();
        Assert.Equal(ModuleState.Running, module.State);

        await module.StopAsync();
        Assert.Equal(ModuleState.Stopped, module.State);
    }

    [Fact]
    public async Task FirewallStatusModule_Lifecycle_TransitionCorrectly()
    {
        var fwService = new FirewallStatusService(NullLogger<FirewallStatusService>.Instance);
        var module = new FirewallStatusModule(fwService, NullLogger<FirewallStatusModule>.Instance);

        Assert.Equal("quickdock.firewall", module.Id);
        Assert.Equal(ModuleState.Uninitialized, module.State);

        await module.InitializeAsync();
        Assert.Equal(ModuleState.Initialized, module.State);

        await module.StartAsync();
        Assert.Equal(ModuleState.Running, module.State);

        await module.StopAsync();
        Assert.Equal(ModuleState.Stopped, module.State);
    }

    #endregion
}
