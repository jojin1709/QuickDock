using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.NetworkSecurity;

public sealed class WindowsSecurityService : IWindowsSecurityService
{
    private readonly ILogger<WindowsSecurityService> _logger;

    public WindowsSecurityService(ILogger<WindowsSecurityService> logger)
    {
        _logger = logger;
    }

    public async Task<WindowsSecurityStatusInfo> GetWindowsSecurityStatusAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            bool isDefenderRunning = false;
            bool realTimeEnabled = true;
            bool avActive = false;
            string avProduct = "Microsoft Defender Antivirus";
            string details = "Active and protecting system";
            bool accessRestricted = false;

            try
            {
                // 1. Check WinDefend service status via sc.exe query
                var scPsi = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = "query WinDefend",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var scProc = Process.Start(scPsi);
                if (scProc != null)
                {
                    string scOutput = scProc.StandardOutput.ReadToEnd();
                    scProc.WaitForExit(2000);
                    if (scOutput.Contains("RUNNING", StringComparison.OrdinalIgnoreCase))
                    {
                        isDefenderRunning = true;
                        avActive = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query service status for WinDefend.");
                accessRestricted = true;
            }

            // 2. Query PowerShell Get-MpComputerStatus for real-time protection state
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                psi.ArgumentList.Add("-NoProfile");
                psi.ArgumentList.Add("-Command");
                psi.ArgumentList.Add("Get-MpComputerStatus | Select-Object -Property AMServiceEnabled,AntivirusEnabled,RealTimeProtectionEnabled | ConvertTo-Json -Compress");

                using var proc = new Process { StartInfo = psi };
                proc.Start();

                if (proc.WaitForExit(4000))
                {
                    string outText = proc.StandardOutput.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(outText) && outText.Contains("RealTimeProtectionEnabled"))
                    {
                        realTimeEnabled = outText.Contains("\"RealTimeProtectionEnabled\":true");
                        if (outText.Contains("\"AntivirusEnabled\":true")) avActive = true;
                        if (outText.Contains("\"AMServiceEnabled\":true")) isDefenderRunning = true;
                    }
                }
                else
                {
                    proc.Kill();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Get-MpComputerStatus query unavailable.");
            }

            if (!isDefenderRunning && !avActive)
            {
                details = accessRestricted
                    ? "Information unavailable due to Windows access restrictions."
                    : "Antivirus service state could not be confirmed.";
            }
            else
            {
                details = $"Antivirus active. Real-time protection: {(realTimeEnabled ? "Enabled" : "Disabled")}.";
            }

            return new WindowsSecurityStatusInfo
            {
                IsDefenderInstalled = true,
                IsRealTimeProtectionEnabled = realTimeEnabled,
                IsAntivirusActive = avActive || isDefenderRunning,
                AntivirusProductName = avProduct,
                SecurityCenterStatus = isDefenderRunning ? "Protected" : "Review Recommended",
                StatusDetails = details,
                HasAccessRestrictions = accessRestricted
            };
        }, cancellationToken);
    }
}
