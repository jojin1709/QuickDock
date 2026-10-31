using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.NetworkSecurity;

public sealed class IpDomainService : IIpDomainService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IpDomainService> _logger;

    public IpDomainService(HttpClient httpClient, ILogger<IpDomainService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IpDomainInfo> ResolveIpDomainAsync(string hostOrIp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hostOrIp))
        {
            return new IpDomainInfo { InputQuery = hostOrIp };
        }

        string clean = hostOrIp.Trim();
        if (clean.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(clean);
                clean = uri.Host;
            }
            catch
            {
                // keep cleaned string
            }
        }

        var v4List = new List<string>();
        var v6List = new List<string>();
        string reverseDns = string.Empty;

        try
        {
            if (IPAddress.TryParse(clean, out var ip))
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork) v4List.Add(ip.ToString());
                else if (ip.AddressFamily == AddressFamily.InterNetworkV6) v6List.Add(ip.ToString());

                try
                {
                    var hostEntry = await Dns.GetHostEntryAsync(ip.ToString(), cancellationToken);
                    reverseDns = hostEntry.HostName;
                }
                catch
                {
                    reverseDns = "No PTR record found";
                }
            }
            else
            {
                var addresses = await Dns.GetHostAddressesAsync(clean, cancellationToken);
                foreach (var a in addresses)
                {
                    if (a.AddressFamily == AddressFamily.InterNetwork) v4List.Add(a.ToString());
                    else if (a.AddressFamily == AddressFamily.InterNetworkV6) v6List.Add(a.ToString());
                }

                if (v4List.Count > 0 && IPAddress.TryParse(v4List[0], out var firstIp))
                {
                    try
                    {
                        var revEntry = await Dns.GetHostEntryAsync(firstIp.ToString(), cancellationToken);
                        reverseDns = revEntry.HostName;
                    }
                    catch
                    {
                        reverseDns = "No PTR record found";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve IP/Domain for {Query}", clean);
        }

        return new IpDomainInfo
        {
            InputQuery = hostOrIp,
            NormalizedDomain = clean,
            Ipv4Addresses = v4List,
            Ipv6Addresses = v6List,
            ReverseDns = reverseDns
        };
    }

    public async Task<PublicIpLookupResult> LookupPublicIpAsync(string provider = "api.ipify.org", CancellationToken cancellationToken = default)
    {
        string endpoint = provider.Contains("://") ? provider : $"https://{provider}";
        var sw = Stopwatch.StartNew();

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, endpoint);
            req.Headers.TryAddWithoutValidation("User-Agent", "QuickDock/1.0");

            using var res = await _httpClient.SendAsync(req, cancellationToken);
            sw.Stop();

            if (res.IsSuccessStatusCode)
            {
                string ip = (await res.Content.ReadAsStringAsync(cancellationToken)).Trim();
                return new PublicIpLookupResult
                {
                    Success = true,
                    PublicIp = ip,
                    Provider = provider,
                    ElapsedMilliseconds = sw.ElapsedMilliseconds
                };
            }

            return new PublicIpLookupResult
            {
                Success = false,
                Provider = provider,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                ErrorMessage = $"External service returned {res.StatusCode}."
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Public IP lookup failed via {Provider}", provider);
            return new PublicIpLookupResult
            {
                Success = false,
                Provider = provider,
                ElapsedMilliseconds = sw.ElapsedMilliseconds,
                ErrorMessage = ex.Message
            };
        }
    }
}
