using System;
using System.Collections.Generic;

namespace QuickDock.Core.Interfaces;

/// <summary>
/// Point-in-time runtime performance snapshot for on-demand developer diagnostics.
/// Zero background polling.
/// </summary>
public sealed record PerformanceSnapshot(
    double ProcessCpuPercentage,
    long WorkingSetBytes,
    long PrivateMemoryBytes,
    int ThreadCount,
    int HandleCount,
    long GcHeapSizeBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    IReadOnlyList<string> ActiveModules,
    IReadOnlyList<string> RegisteredModules,
    TimeSpan Uptime,
    DateTimeOffset Timestamp
)
{
    public double WorkingSetMegabytes => Math.Round(WorkingSetBytes / (1024.0 * 1024.0), 2);
    public double PrivateMemoryMegabytes => Math.Round(PrivateMemoryBytes / (1024.0 * 1024.0), 2);
    public double GcHeapMegabytes => Math.Round(GcHeapSizeBytes / (1024.0 * 1024.0), 2);
}

/// <summary>
/// On-demand performance diagnostics interface.
/// </summary>
public interface IPerformanceDiagnosticsService
{
    /// <summary>
    /// Samples runtime performance counters and GC heap state at the moment of invocation.
    /// </summary>
    PerformanceSnapshot GetSnapshot();
}

