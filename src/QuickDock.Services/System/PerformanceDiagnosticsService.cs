using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Modules;

namespace QuickDock.Services.System;

/// <summary>
/// Implements on-demand performance and memory inspection without permanent background workers.
/// </summary>
public sealed class PerformanceDiagnosticsService : IPerformanceDiagnosticsService
{
    private readonly IModuleManager _moduleManager;
    private readonly ILogger<PerformanceDiagnosticsService> _logger;
    private readonly Process _currentProcess;
    private readonly DateTime _processStartTime;
    private TimeSpan _lastCpuTime;
    private DateTimeOffset _lastSampleTime;

    public PerformanceDiagnosticsService(
        IModuleManager moduleManager,
        ILogger<PerformanceDiagnosticsService> logger)
    {
        _moduleManager = moduleManager ?? throw new ArgumentNullException(nameof(moduleManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _currentProcess = Process.GetCurrentProcess();
        _processStartTime = _currentProcess.StartTime;
        _lastCpuTime = _currentProcess.TotalProcessorTime;
        _lastSampleTime = DateTimeOffset.UtcNow;
    }

    public PerformanceSnapshot GetSnapshot()
    {
        _currentProcess.Refresh();

        var now = DateTimeOffset.UtcNow;
        var currentCpuTime = _currentProcess.TotalProcessorTime;
        var elapsedWallClock = (now - _lastSampleTime).TotalMilliseconds;

        double cpuPercent = 0.0;
        if (elapsedWallClock > 0)
        {
            var cpuUsedMs = (currentCpuTime - _lastCpuTime).TotalMilliseconds;
            cpuPercent = Math.Round((cpuUsedMs / (elapsedWallClock * Environment.ProcessorCount)) * 100.0, 2);
            cpuPercent = Math.Clamp(cpuPercent, 0.0, 100.0);
        }

        _lastCpuTime = currentCpuTime;
        _lastSampleTime = now;

        var activeModules = _moduleManager.Modules
            .Where(m => m.State == ModuleState.Running || m.State == ModuleState.Starting)
            .Select(m => m.Name)
            .ToList();

        var registeredModules = _moduleManager.Modules
            .Select(m => $"{m.Name} ({m.State})")
            .ToList();

        return new PerformanceSnapshot(
            ProcessCpuPercentage: cpuPercent,
            WorkingSetBytes: _currentProcess.WorkingSet64,
            PrivateMemoryBytes: _currentProcess.PrivateMemorySize64,
            ThreadCount: _currentProcess.Threads.Count,
            HandleCount: _currentProcess.HandleCount,
            GcHeapSizeBytes: GC.GetTotalMemory(forceFullCollection: false),
            Gen0Collections: GC.CollectionCount(0),
            Gen1Collections: GC.CollectionCount(1),
            Gen2Collections: GC.CollectionCount(2),
            ActiveModules: activeModules,
            RegisteredModules: registeredModules,
            Uptime: DateTime.Now - _processStartTime,
            Timestamp: now
        );
    }
}

