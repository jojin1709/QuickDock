using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.NetworkSecurity;

public sealed class LocalSecurityCheckService : ILocalSecurityCheckService
{
    private readonly IFirewallStatusService _firewallService;
    private readonly INetworkConnectionService _connectionService;
    private readonly IWindowsSecurityService _windowsSecurityService;
    private readonly ILogger<LocalSecurityCheckService> _logger;

    public LocalSecurityCheckService(
        IFirewallStatusService firewallService,
        INetworkConnectionService connectionService,
        IWindowsSecurityService windowsSecurityService,
        ILogger<LocalSecurityCheckService> logger)
    {
        _firewallService = firewallService;
        _connectionService = connectionService;
        _windowsSecurityService = windowsSecurityService;
        _logger = logger;
    }

    public async Task<LocalSecurityCheckResult> RunLocalSecurityChecksAsync(CancellationToken cancellationToken = default)
    {
        var findings = new List<SecurityFinding>();

        // 1. Account & Elevation Check
        bool isAdmin = false;
        string userName = Environment.UserName;
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate administrator elevation.");
        }

        if (isAdmin)
        {
            findings.Add(new SecurityFinding
            {
                Id = "SEC-ACC-001",
                Title = "QuickDock Running as Administrator",
                Category = "Account",
                Severity = FindingSeverity.Low,
                Description = "The application is currently running with elevated Administrator privileges.",
                Recommendation = "Run daily productivity tools under standard non-elevated user context unless elevation is specifically required."
            });
        }
        else
        {
            findings.Add(new SecurityFinding
            {
                Id = "SEC-ACC-002",
                Title = "Standard User Context",
                Category = "Account",
                Severity = FindingSeverity.Informational,
                Description = $"Application running under standard user permissions ({userName}).",
                Recommendation = "No action required."
            });
        }

        // 2. UAC Check
        bool isUac = true;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
            var val = key?.GetValue("EnableLUA");
            if (val is int intVal && intVal == 0)
            {
                isUac = false;
                findings.Add(new SecurityFinding
                {
                    Id = "SEC-UAC-001",
                    Title = "User Account Control (UAC) Disabled",
                    Category = "System",
                    Severity = FindingSeverity.High,
                    Description = "UAC (EnableLUA) is currently disabled on this system. Applications can elevate without notification.",
                    Recommendation = "Enable User Account Control via Control Panel or Settings to prevent silent privilege escalation."
                });
            }
        }
        catch
        {
            // Access to registry denied
        }

        if (isUac)
        {
            findings.Add(new SecurityFinding
            {
                Id = "SEC-UAC-002",
                Title = "User Account Control (UAC) Active",
                Category = "System",
                Severity = FindingSeverity.Informational,
                Description = "UAC is active and prompting for elevation consent.",
                Recommendation = "No action required."
            });
        }

        // 3. Secure Boot Check
        bool isSecureBoot = false;
        try
        {
            using var sbKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            var sbVal = sbKey?.GetValue("UEFISecureBootEnabled");
            if (sbVal is int s && s == 1)
            {
                isSecureBoot = true;
                findings.Add(new SecurityFinding
                {
                    Id = "SEC-BOOT-001",
                    Title = "UEFI Secure Boot Enabled",
                    Category = "System",
                    Severity = FindingSeverity.Informational,
                    Description = "UEFI Secure Boot is active and protecting bootloader integrity.",
                    Recommendation = "No action required."
                });
            }
            else
            {
                findings.Add(new SecurityFinding
                {
                    Id = "SEC-BOOT-002",
                    Title = "UEFI Secure Boot Not Confirmed",
                    Category = "System",
                    Severity = FindingSeverity.Low,
                    Description = "Secure Boot state could not be verified as active (legacy BIOS or disabled in firmware).",
                    Recommendation = "Enable Secure Boot in UEFI firmware settings if supported by your hardware."
                });
            }
        }
        catch
        {
            // ignore
        }

        // 4. Firewall Status Check
        var fwStatus = await _firewallService.GetFirewallStatusAsync(cancellationToken);
        if (fwStatus.IsAccessible)
        {
            foreach (var p in fwStatus.Profiles)
            {
                if (!p.IsEnabled)
                {
                    findings.Add(new SecurityFinding
                    {
                        Id = $"SEC-FW-{p.ProfileName.ToUpperInvariant()}",
                        Title = $"Windows Firewall Disabled on {p.ProfileName} Profile",
                        Category = "Network",
                        Severity = FindingSeverity.Medium,
                        Description = $"Windows Defender Firewall is turned off for the {p.ProfileName} network profile.",
                        Recommendation = $"Enable the firewall on the {p.ProfileName} profile to prevent unsolicited inbound network traffic."
                    });
                }
            }
        }

        // 5. Antivirus / Defender Status Check
        var winSec = await _windowsSecurityService.GetWindowsSecurityStatusAsync(cancellationToken);
        if (!winSec.IsAntivirusActive)
        {
            findings.Add(new SecurityFinding
            {
                Id = "SEC-AV-001",
                Title = "Antivirus Protection Status Inactive",
                Category = "System",
                Severity = FindingSeverity.Medium,
                Description = "Antivirus real-time protection could not be verified as active.",
                Recommendation = "Ensure Microsoft Defender or a trusted endpoint protection suite is running with real-time scanning enabled."
            });
        }

        // 6. Developer Exposure: Listening on 0.0.0.0
        var listeningSockets = await _connectionService.GetListeningSocketsAsync(cancellationToken);
        var devPorts = new HashSet<int> { 3000, 5000, 8000, 8080, 8443, 3306, 5432, 6379, 27017 };

        foreach (var s in listeningSockets)
        {
            if (devPorts.Contains(s.Port))
            {
                bool isWorldBound = s.LocalAddress == "0.0.0.0" || s.LocalAddress == "::";
                if (isWorldBound)
                {
                    findings.Add(new SecurityFinding
                    {
                        Id = $"SEC-PORT-{s.Port}",
                        Title = $"Developer Port {s.Port} Exposed on All Interfaces (0.0.0.0)",
                        Category = "Network",
                        Severity = FindingSeverity.Medium,
                        Description = $"Process '{s.ProcessName}' (PID {s.ProcessId}) is listening on {s.Protocol} port {s.Port} across all network adapters.",
                        Recommendation = "Bind development servers to localhost (127.0.0.1) instead of 0.0.0.0 to prevent exposure on untrusted local networks."
                    });
                }
                else
                {
                    findings.Add(new SecurityFinding
                    {
                        Id = $"SEC-PORT-{s.Port}",
                        Title = $"Developer Port {s.Port} Listening on Localhost",
                        Category = "Network",
                        Severity = FindingSeverity.Informational,
                        Description = $"Process '{s.ProcessName}' (PID {s.ProcessId}) is listening on {s.LocalAddress}:{s.Port}.",
                        Recommendation = "No action required."
                    });
                }
            }
        }

        return new LocalSecurityCheckResult
        {
            ScanTimestamp = DateTime.UtcNow,
            CurrentUser = userName,
            IsAdministrator = isAdmin,
            IsUacEnabled = isUac,
            IsSecureBootEnabled = isSecureBoot,
            Findings = findings
        };
    }
}
