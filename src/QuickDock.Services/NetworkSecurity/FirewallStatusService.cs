using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.NetworkSecurity;

public sealed class FirewallStatusService : IFirewallStatusService
{
    private readonly ILogger<FirewallStatusService> _logger;

    public FirewallStatusService(ILogger<FirewallStatusService> logger)
    {
        _logger = logger;
    }

    public async Task<FirewallStatusInfo> GetFirewallStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh.exe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            psi.ArgumentList.Add("advfirewall");
            psi.ArgumentList.Add("show");
            psi.ArgumentList.Add("allprofiles");

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            string output = await stdoutTask;

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                return new FirewallStatusInfo
                {
                    IsAccessible = false,
                    SummaryText = "Unable to read Windows Firewall status.",
                    ErrorMessage = "Process exited with non-zero code."
                };
            }

            var profiles = ParseNetshFirewallOutput(output);
            bool allEnabled = profiles.TrueForAll(p => p.IsEnabled);

            string summary = allEnabled
                ? "Windows Firewall is active on all profiles (Domain, Private, Public)."
                : "Windows Firewall has one or more profiles disabled.";

            return new FirewallStatusInfo
            {
                IsAccessible = true,
                Profiles = profiles,
                SummaryText = summary
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inspect Windows Firewall status.");
            return new FirewallStatusInfo
            {
                IsAccessible = false,
                SummaryText = "Windows Firewall status unavailable.",
                ErrorMessage = ex.Message
            };
        }
    }

    private static List<FirewallProfileStatus> ParseNetshFirewallOutput(string output)
    {
        var list = new List<FirewallProfileStatus>();
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        string currentProfile = string.Empty;
        bool isEnabled = false;
        string inbound = "Block";
        string outbound = "Allow";

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();

            if (line.Contains("Domain Profile Settings", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(currentProfile))
                    list.Add(new FirewallProfileStatus { ProfileName = currentProfile, IsEnabled = isEnabled, InboundAction = inbound, OutboundAction = outbound });
                currentProfile = "Domain";
            }
            else if (line.Contains("Private Profile Settings", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(currentProfile))
                    list.Add(new FirewallProfileStatus { ProfileName = currentProfile, IsEnabled = isEnabled, InboundAction = inbound, OutboundAction = outbound });
                currentProfile = "Private";
            }
            else if (line.Contains("Public Profile Settings", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(currentProfile))
                    list.Add(new FirewallProfileStatus { ProfileName = currentProfile, IsEnabled = isEnabled, InboundAction = inbound, OutboundAction = outbound });
                currentProfile = "Public";
            }
            else if (line.StartsWith("State", StringComparison.OrdinalIgnoreCase))
            {
                isEnabled = line.Contains("ON", StringComparison.OrdinalIgnoreCase);
            }
            else if (line.StartsWith("Firewall Policy", StringComparison.OrdinalIgnoreCase))
            {
                if (line.Contains("BlockInbound", StringComparison.OrdinalIgnoreCase)) inbound = "BlockInbound";
                else if (line.Contains("AllowInbound", StringComparison.OrdinalIgnoreCase)) inbound = "AllowInbound";

                if (line.Contains("AllowOutbound", StringComparison.OrdinalIgnoreCase)) outbound = "AllowOutbound";
                else if (line.Contains("BlockOutbound", StringComparison.OrdinalIgnoreCase)) outbound = "BlockOutbound";
            }
        }

        if (!string.IsNullOrEmpty(currentProfile))
        {
            list.Add(new FirewallProfileStatus { ProfileName = currentProfile, IsEnabled = isEnabled, InboundAction = inbound, OutboundAction = outbound });
        }

        return list;
    }
}
