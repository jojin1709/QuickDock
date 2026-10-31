using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.Developer;

public sealed class DeveloperProcessService : IDeveloperProcessService
{
    private readonly ILocalDevelopmentService _localDevService;
    private readonly ILogger<DeveloperProcessService> _logger;

    public DeveloperProcessService(ILocalDevelopmentService localDevService, ILogger<DeveloperProcessService> logger)
    {
        _localDevService = localDevService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DeveloperProcessInfo>> GetDeveloperProcessesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Get listening ports to correlate
        var listeningPorts = await _localDevService.GetListeningPortsAsync(cancellationToken);
        var pidToPorts = new Dictionary<int, List<int>>();
        foreach (var lp in listeningPorts)
        {
            if (!pidToPorts.TryGetValue(lp.ProcessId, out var list))
            {
                list = [];
                pidToPorts[lp.ProcessId] = list;
            }
            list.Add(lp.Port);
        }

        return await Task.Run<IReadOnlyList<DeveloperProcessInfo>>(() =>
        {
            var processes = Process.GetProcesses();
            var results = new List<DeveloperProcessInfo>(processes.Length);

            foreach (var proc in processes)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    int pid = proc.Id;
                    string name = proc.ProcessName;
                    long workingSet = proc.WorkingSet64;
                    DateTime? startTime = null;
                    string mainModule = string.Empty;
                    bool accessible = true;

                    try
                    {
                        startTime = proc.StartTime;
                        mainModule = proc.MainModule?.FileName ?? string.Empty;
                    }
                    catch
                    {
                        accessible = false;
                    }

                    pidToPorts.TryGetValue(pid, out var ports);

                    results.Add(new DeveloperProcessInfo
                    {
                        ProcessId = pid,
                        ProcessName = name,
                        WorkingSetBytes = workingSet,
                        StartTime = startTime,
                        MainModulePath = mainModule,
                        ListeningPorts = ports ?? [],
                        IsAccessible = accessible
                    });
                }
                catch
                {
                    // Process exited during enumeration
                }
                finally
                {
                    proc.Dispose();
                }
            }

            // Order by processes that have listening ports first, then by memory descending
            return results
                .OrderByDescending(p => p.ListeningPorts.Count > 0)
                .ThenByDescending(p => p.WorkingSetBytes)
                .ToList();
        }, cancellationToken);
    }

    public bool TerminateProcess(int processId)
    {
        if (processId <= 4) return false; // Prevent killing system processes (Idle, System)

        try
        {
            using var proc = Process.GetProcessById(processId);
            proc.Kill(entireProcessTree: true);
            _logger.LogInformation("Process {Pid} terminated by user.", processId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to terminate process {Pid}", processId);
            return false;
        }
    }
}
