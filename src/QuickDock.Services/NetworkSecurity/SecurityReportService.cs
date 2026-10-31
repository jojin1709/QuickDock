using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.NetworkSecurity;

public sealed class SecurityReportService : ISecurityReportService
{
    private readonly INetworkOverviewService _networkService;
    private readonly INetworkConnectionService _connectionService;
    private readonly IFirewallStatusService _firewallService;
    private readonly ILocalSecurityCheckService _securityCheckService;
    private readonly ILogger<SecurityReportService> _logger;

    public SecurityReportService(
        INetworkOverviewService networkService,
        INetworkConnectionService connectionService,
        IFirewallStatusService firewallService,
        ILocalSecurityCheckService securityCheckService,
        ILogger<SecurityReportService> logger)
    {
        _networkService = networkService;
        _connectionService = connectionService;
        _firewallService = firewallService;
        _securityCheckService = securityCheckService;
        _logger = logger;
    }

    public async Task<SecurityReportResult> GenerateReportAsync(SecurityReportRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var overview = await _networkService.GetNetworkOverviewAsync(cancellationToken);
            var adapters = request.IncludeNetworkAdapters ? await _networkService.GetAdaptersAsync(cancellationToken) : [];
            var listeningPorts = request.IncludeListeningPorts ? await _connectionService.GetListeningSocketsAsync(cancellationToken) : [];
            var connections = request.IncludeActiveConnections ? await _connectionService.GetActiveConnectionsAsync(cancellationToken) : [];
            var firewall = request.IncludeFirewallStatus ? await _firewallService.GetFirewallStatusAsync(cancellationToken) : null;
            var checks = request.IncludeSecurityChecks ? await _securityCheckService.RunLocalSecurityChecksAsync(cancellationToken) : null;

            string content;
            if (request.Format == SecurityReportFormat.Json)
            {
                var payload = new
                {
                    GeneratedAt = DateTime.UtcNow,
                    Disclaimer = "This diagnostic report contains local network and system configuration details. Share with caution.",
                    NetworkOverview = overview,
                    Adapters = adapters,
                    ListeningPorts = listeningPorts,
                    ActiveConnections = connections,
                    Firewall = firewall,
                    SecurityChecks = checks
                };

                content = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine("                      QUICKDOCK NETWORK & SECURITY REPORT                       ");
                sb.AppendLine("================================================================================");
                sb.AppendLine($"Generated:  {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                sb.AppendLine($"Computer:   {overview.ComputerName}");
                sb.AppendLine($"Hostname:   {overview.Hostname}");
                sb.AppendLine($"Primary IP: {overview.PrimaryIpv4}");
                sb.AppendLine($"Gateway:    {overview.DefaultGateway}");
                sb.AppendLine("DISCLAIMER: This diagnostic report contains sensitive local system and network configuration details.");
                sb.AppendLine("================================================================================");
                sb.AppendLine();

                // Security Findings
                if (checks != null)
                {
                    sb.AppendLine("--- [ LOCAL SECURITY CHECKS ] ---");
                    sb.AppendLine($"Account Context:  {checks.CurrentUser} (Admin: {checks.IsAdministrator})");
                    sb.AppendLine($"UAC Active:       {checks.IsUacEnabled}");
                    sb.AppendLine($"Secure Boot:      {checks.IsSecureBootEnabled}");
                    sb.AppendLine($"Findings Summary: {checks.HighCount} High, {checks.MediumCount} Medium, {checks.LowCount} Low, {checks.InfoCount} Info");
                    sb.AppendLine();

                    foreach (var f in checks.Findings)
                    {
                        sb.AppendLine($"[{f.Severity.ToString().ToUpperInvariant()}] {f.Id}: {f.Title}");
                        sb.AppendLine($"   Category:       {f.Category}");
                        sb.AppendLine($"   Description:    {f.Description}");
                        sb.AppendLine($"   Recommendation: {f.Recommendation}");
                        sb.AppendLine();
                    }
                }

                // Firewall Status
                if (firewall != null)
                {
                    sb.AppendLine("--- [ WINDOWS FIREWALL STATUS ] ---");
                    sb.AppendLine($"Summary: {firewall.SummaryText}");
                    foreach (var p in firewall.Profiles)
                    {
                        sb.AppendLine($" • {p.ProfileName} Profile: {(p.IsEnabled ? "ENABLED (ON)" : "DISABLED (OFF)")} [In: {p.InboundAction}, Out: {p.OutboundAction}]");
                    }
                    sb.AppendLine();
                }

                // Listening Ports
                if (listeningPorts.Count > 0)
                {
                    sb.AppendLine("--- [ LISTENING PORTS ] ---");
                    sb.AppendLine(string.Format("{0,-8} {1,-18} {2,-8} {3,-24} {4}", "PROTO", "ADDRESS", "PORT", "PROCESS", "PID"));
                    sb.AppendLine(new string('-', 75));
                    foreach (var s in listeningPorts)
                    {
                        sb.AppendLine(string.Format("{0,-8} {1,-18} {2,-8} {3,-24} {4}", s.Protocol, s.LocalAddress, s.Port, s.ProcessName, s.ProcessId));
                    }
                    sb.AppendLine();
                }

                // Active Connections
                if (connections.Count > 0)
                {
                    sb.AppendLine("--- [ ACTIVE LOCAL CONNECTIONS ] ---");
                    sb.AppendLine(string.Format("{0,-8} {1,-22} {2,-22} {3,-14} {4}", "PROTO", "LOCAL", "REMOTE", "STATE", "PROCESS"));
                    sb.AppendLine(new string('-', 78));
                    foreach (var c in connections)
                    {
                        sb.AppendLine(string.Format("{0,-8} {1,-22} {2,-22} {3,-14} {4}", c.Protocol, c.LocalEndpoint, c.RemoteEndpoint, c.State, c.ProcessName));
                    }
                    sb.AppendLine();
                }

                // Adapters
                if (adapters.Count > 0)
                {
                    sb.AppendLine("--- [ NETWORK ADAPTERS ] ---");
                    foreach (var a in adapters)
                    {
                        sb.AppendLine($"• {a.Name} ({a.InterfaceType}) - Status: {a.Status}, Speed: {a.SpeedFormatted}");
                        sb.AppendLine($"  MAC: {a.MacAddress}, DHCP: {(a.IsDhcpEnabled ? "Enabled" : "Disabled")}");
                        foreach (var ip in a.IpAddresses)
                        {
                            sb.AppendLine($"  IP: {ip.Address} ({ip.Family}, {ip.Category})");
                        }
                        if (a.Gateways.Count > 0) sb.AppendLine($"  Gateway: {string.Join(", ", a.Gateways)}");
                        if (a.DnsServers.Count > 0) sb.AppendLine($"  DNS:     {string.Join(", ", a.DnsServers)}");
                        sb.AppendLine();
                    }
                }

                content = sb.ToString();
            }

            return new SecurityReportResult
            {
                Success = true,
                Format = request.Format,
                ReportContent = content,
                CreatedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate security report.");
            return new SecurityReportResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}

