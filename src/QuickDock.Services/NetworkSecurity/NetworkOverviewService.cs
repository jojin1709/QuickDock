using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.NetworkSecurity;

public sealed class NetworkOverviewService : INetworkOverviewService
{
    private readonly ILogger<NetworkOverviewService> _logger;

    public NetworkOverviewService(ILogger<NetworkOverviewService> logger)
    {
        _logger = logger;
    }

    public Task<NetworkOverviewInfo> GetNetworkOverviewAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            string hostname = Environment.MachineName;
            try
            {
                hostname = Dns.GetHostName();
            }
            catch
            {
                // fallback to MachineName
            }

            string primaryIface = "None";
            string connType = "Unknown";
            string opStatus = "Disconnected";
            string primaryIpv4 = string.Empty;
            string primaryIpv6 = string.Empty;
            string gateway = string.Empty;
            var dnsServers = new List<string>();
            bool isDhcp = false;
            string mac = string.Empty;
            string speed = string.Empty;

            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                NetworkInterface? bestInterface = null;

                foreach (var iface in interfaces)
                {
                    if (iface.OperationalStatus == OperationalStatus.Up &&
                        iface.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        iface.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    {
                        var ipProps = iface.GetIPProperties();
                        if (ipProps.GatewayAddresses.Count > 0)
                        {
                            bestInterface = iface;
                            break;
                        }
                        bestInterface ??= iface;
                    }
                }

                if (bestInterface != null)
                {
                    primaryIface = bestInterface.Name;
                    connType = bestInterface.NetworkInterfaceType.ToString();
                    opStatus = bestInterface.OperationalStatus.ToString();
                    speed = FormatSpeed(bestInterface.Speed);
                    mac = FormatMac(bestInterface.GetPhysicalAddress());

                    var ipProps = bestInterface.GetIPProperties();

                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork && string.IsNullOrEmpty(primaryIpv4))
                        {
                            primaryIpv4 = addr.Address.ToString();
                        }
                        else if (addr.Address.AddressFamily == AddressFamily.InterNetworkV6 && string.IsNullOrEmpty(primaryIpv6))
                        {
                            primaryIpv6 = addr.Address.ToString();
                        }
                    }

                    if (ipProps.GatewayAddresses.Count > 0)
                    {
                        gateway = ipProps.GatewayAddresses[0].Address.ToString();
                    }

                    foreach (var dns in ipProps.DnsAddresses)
                    {
                        dnsServers.Add(dns.ToString());
                    }

                    try
                    {
                        var v4Props = ipProps.GetIPv4Properties();
                        isDhcp = v4Props?.IsDhcpEnabled ?? false;
                    }
                    catch
                    {
                        // Some virtual interfaces fail on GetIPv4Properties
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error gathering network overview.");
            }

            return new NetworkOverviewInfo
            {
                Hostname = hostname,
                ComputerName = Environment.MachineName,
                PrimaryInterfaceName = primaryIface,
                ConnectionType = connType,
                OperationalStatus = opStatus,
                PrimaryIpv4 = primaryIpv4,
                PrimaryIpv6 = primaryIpv6,
                DefaultGateway = gateway,
                DnsServers = dnsServers,
                IsDhcpEnabled = isDhcp,
                MacAddress = mac,
                LinkSpeed = speed
            };
        }, cancellationToken);
    }

    public Task<IReadOnlyList<NetworkAdapterDetail>> GetAdaptersAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<NetworkAdapterDetail>>(() =>
        {
            var list = new List<NetworkAdapterDetail>();

            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var iface in interfaces)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    try
                    {
                        var ipDetails = new List<IpAddressDetail>();
                        var gateways = new List<string>();
                        var dnsList = new List<string>();
                        bool isDhcp = false;

                        var ipProps = iface.GetIPProperties();

                        foreach (var uni in ipProps.UnicastAddresses)
                        {
                            var cat = CategorizeAddress(uni.Address);
                            ipDetails.Add(new IpAddressDetail
                            {
                                Address = uni.Address.ToString(),
                                Family = uni.Address.AddressFamily == AddressFamily.InterNetworkV6 ? "IPv6" : "IPv4",
                                Category = cat
                            });
                        }

                        foreach (var gw in ipProps.GatewayAddresses)
                        {
                            gateways.Add(gw.Address.ToString());
                        }

                        foreach (var dns in ipProps.DnsAddresses)
                        {
                            dnsList.Add(dns.ToString());
                        }

                        try
                        {
                            isDhcp = ipProps.GetIPv4Properties()?.IsDhcpEnabled ?? false;
                        }
                        catch
                        {
                            // ignore for non-v4
                        }

                        list.Add(new NetworkAdapterDetail
                        {
                            Id = iface.Id,
                            Name = iface.Name,
                            Description = iface.Description,
                            Status = iface.OperationalStatus.ToString(),
                            InterfaceType = iface.NetworkInterfaceType.ToString(),
                            MacAddress = FormatMac(iface.GetPhysicalAddress()),
                            IpAddresses = ipDetails,
                            Gateways = gateways,
                            DnsServers = dnsList,
                            IsDhcpEnabled = isDhcp,
                            SpeedFormatted = FormatSpeed(iface.Speed)
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to inspect adapter {Name}", iface.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enumerating network adapters.");
            }

            return list;
        }, cancellationToken);
    }

    public static IpAddressCategory CategorizeAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return IpAddressCategory.Loopback;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] bytes = address.GetAddressBytes();
            // Link-local: 169.254.0.0/16
            if (bytes[0] == 169 && bytes[1] == 254) return IpAddressCategory.LinkLocal;
            // Private: 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16
            if (bytes[0] == 10) return IpAddressCategory.Private;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return IpAddressCategory.Private;
            if (bytes[0] == 192 && bytes[1] == 168) return IpAddressCategory.Private;

            return IpAddressCategory.Public;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.IsIPv6LinkLocal) return IpAddressCategory.LinkLocal;
            if (address.IsIPv6SiteLocal) return IpAddressCategory.Private;

            byte[] bytes = address.GetAddressBytes();
            // Unique Local: fc00::/7
            if ((bytes[0] & 0xFE) == 0xFC) return IpAddressCategory.Private;

            return IpAddressCategory.Public;
        }

        return IpAddressCategory.Public;
    }

    private static string FormatMac(PhysicalAddress? mac)
    {
        if (mac == null) return string.Empty;
        byte[] bytes = mac.GetAddressBytes();
        if (bytes.Length == 0) return string.Empty;
        return string.Join("-", Array.ConvertAll(bytes, b => b.ToString("X2")));
    }

    private static string FormatSpeed(long speedBitsPerSec)
    {
        if (speedBitsPerSec <= 0) return "Unknown";
        if (speedBitsPerSec >= 1_000_000_000)
            return $"{Math.Round(speedBitsPerSec / 1_000_000_000.0, 1)} Gbps";
        if (speedBitsPerSec >= 1_000_000)
            return $"{Math.Round(speedBitsPerSec / 1_000_000.0, 1)} Mbps";
        return $"{speedBitsPerSec} bps";
    }
}

