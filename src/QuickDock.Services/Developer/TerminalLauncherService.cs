using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.Developer;

public sealed class TerminalLauncherService : ITerminalLauncherService
{
    private readonly ILogger<TerminalLauncherService> _logger;

    public TerminalLauncherService(ILogger<TerminalLauncherService> logger)
    {
        _logger = logger;
    }

    public bool CanLaunch(TerminalType type)
    {
        return type switch
        {
            TerminalType.WindowsTerminal => FindExecutableInPath("wt.exe") != null,
            TerminalType.PowerShell => FindExecutableInPath("pwsh.exe") != null || FindExecutableInPath("powershell.exe") != null,
            TerminalType.CommandPrompt => FindExecutableInPath("cmd.exe") != null,
            _ => false
        };
    }

    public bool LaunchTerminal(TerminalLaunchRequest request)
    {
        string workDir = !string.IsNullOrWhiteSpace(request.WorkingDirectory) && Directory.Exists(request.WorkingDirectory)
            ? request.WorkingDirectory
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        try
        {
            var psi = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = workDir
            };

            if (request.AsAdministrator)
            {
                psi.Verb = "runas";
            }

            switch (request.Type)
            {
                case TerminalType.WindowsTerminal:
                    psi.FileName = "wt.exe";
                    psi.Arguments = $"-d \"{workDir}\"";
                    break;

                case TerminalType.PowerShell:
                    string psExe = FindExecutableInPath("pwsh.exe") ?? "powershell.exe";
                    psi.FileName = psExe;
                    psi.Arguments = "-NoExit";
                    break;

                case TerminalType.CommandPrompt:
                default:
                    psi.FileName = "cmd.exe";
                    psi.Arguments = $"/K \"cd /d \"{workDir}\"\"";
                    break;
            }

            _logger.LogInformation("Launching terminal {Type} at {WorkDir}", request.Type, workDir);
            using var proc = Process.Start(psi);
            return proc != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch terminal {Type} at {WorkDir}", request.Type, workDir);
            return false;
        }
    }

    private static string? FindExecutableInPath(string exeName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in paths)
        {
            try
            {
                var candidate = Path.Combine(p.Trim('"', ' '), exeName);
                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
                // ignore
            }
        }

        // System32 fallback
        var sys32 = Path.Combine(Environment.SystemDirectory, exeName);
        if (File.Exists(sys32)) return sys32;

        return null;
    }
}

