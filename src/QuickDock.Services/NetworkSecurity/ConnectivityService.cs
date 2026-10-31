using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.NetworkSecurity;

public sealed class ConnectivityService : IConnectivityService
{
    private readonly ILogger<ConnectivityService> _logger;

    public ConnectivityService(ILogger<ConnectivityService> logger)
    {
        _logger = logger;
    }

    public async Task<ConnectivityDiagnosticsResult> PingAsync(
        string hostOrIp,
        int count = 4,
        int timeoutMs = 2000,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hostOrIp))
        {
            return new ConnectivityDiagnosticsResult
            {
                TargetHost = hostOrIp,
                ErrorMessage = "Target host or IP cannot be empty."
            };
        }

        string cleanHost = hostOrIp.Trim();
        int safeCount = Math.Clamp(count, 1, 10);
        int safeTimeout = Math.Clamp(timeoutMs, 500, 10000);

        var echoes = new List<PingEchoResult>();
        string resolvedIp = string.Empty;
        long totalRtt = 0;
        int receivedCount = 0;

        using var pinger = new Ping();
        byte[] buffer = new byte[32];
        new Random(42).NextBytes(buffer);

        for (int i = 0; i < safeCount; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var reply = await pinger.SendPingAsync(cleanHost, safeTimeout, buffer);
                if (reply != null)
                {
                    bool isSuccess = reply.Status == IPStatus.Success;
                    if (isSuccess) receivedCount++;
                    totalRtt += reply.RoundtripTime;

                    if (reply.Address != null && string.IsNullOrEmpty(resolvedIp))
                    {
                        resolvedIp = reply.Address.ToString();
                    }

                    echoes.Add(new PingEchoResult
                    {
                        Sequence = i + 1,
                        Success = isSuccess,
                        IpAddress = reply.Address?.ToString() ?? cleanHost,
                        RoundTripTimeMs = reply.RoundtripTime,
                        Ttl = reply.Options?.Ttl ?? 0,
                        Status = reply.Status.ToString()
                    });
                }
            }
            catch (Exception ex)
            {
                echoes.Add(new PingEchoResult
                {
                    Sequence = i + 1,
                    Success = false,
                    IpAddress = cleanHost,
                    Status = ex.Message
                });
            }

            if (i < safeCount - 1)
            {
                try
                {
                    await Task.Delay(200, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        double avgRtt = receivedCount > 0 ? Math.Round((double)totalRtt / receivedCount, 1) : 0;
        bool reachable = receivedCount > 0;

        string summary = reachable
            ? $"Target reachable: {receivedCount}/{safeCount} packets received (Avg RTT: {avgRtt} ms)."
            : $"Target unreachable: 0/{safeCount} packets received.";

        return new ConnectivityDiagnosticsResult
        {
            TargetHost = cleanHost,
            ResolvedIp = resolvedIp,
            IsReachable = reachable,
            Echoes = echoes,
            AverageRttMs = avgRtt,
            PacketsSent = echoes.Count,
            PacketsReceived = receivedCount,
            SummaryText = summary
        };
    }

    public async Task<bool> TestGatewayConnectivityAsync(CancellationToken cancellationToken = default)
    {
        string? gateway = null;
        try
        {
            foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (iface.OperationalStatus == OperationalStatus.Up)
                {
                    var gwList = iface.GetIPProperties().GatewayAddresses;
                    if (gwList.Count > 0)
                    {
                        gateway = gwList[0].Address.ToString();
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve default gateway.");
        }

        if (string.IsNullOrEmpty(gateway)) return false;

        var result = await PingAsync(gateway, count: 2, timeoutMs: 1500, cancellationToken: cancellationToken);
        return result.IsReachable;
    }
}
