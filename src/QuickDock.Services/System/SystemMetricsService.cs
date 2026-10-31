using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using QuickDock.Infrastructure.Windows;

namespace QuickDock.Services.System;

public sealed class SystemMetricsService : ISystemMetricsService
{
    private readonly ILogger<SystemMetricsService> _logger;
    private long _lastIdleTime;
    private long _lastKernelTime;
    private long _lastUserTime;
    private DateTimeOffset _lastSampleTime = DateTimeOffset.MinValue;
    private double? _lastCalculatedCpu;
    private string? _cachedCpuModel;

    public SystemMetricsService(ILogger<SystemMetricsService> logger)
    {
        _logger = logger;
        // Warm up initial CPU sample
        NativeMethods.GetSystemTimes(out _lastIdleTime, out _lastKernelTime, out _lastUserTime);
        _lastSampleTime = DateTimeOffset.UtcNow;
    }

    public double? GetCpuUsagePercentage()
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            if ((now - _lastSampleTime).TotalMilliseconds < 500 && _lastCalculatedCpu.HasValue)
            {
                return _lastCalculatedCpu;
            }

            if (!NativeMethods.GetSystemTimes(out long idleTime, out long kernelTime, out long userTime))
            {
                return null;
            }

            long usrDiff = userTime - _lastUserTime;
            long kerDiff = kernelTime - _lastKernelTime;
            long idlDiff = idleTime - _lastIdleTime;

            long total = usrDiff + kerDiff;
            if (total <= 0)
            {
                return _lastCalculatedCpu;
            }

            long busy = total - idlDiff;
            if (busy < 0) busy = 0;

            double cpuPercent = (busy * 100.0) / total;
            if (cpuPercent > 100.0) cpuPercent = 100.0;
            if (cpuPercent < 0.0) cpuPercent = 0.0;

            _lastIdleTime = idleTime;
            _lastKernelTime = kernelTime;
            _lastUserTime = userTime;
            _lastSampleTime = now;
            _lastCalculatedCpu = Math.Round(cpuPercent, 1);

            return _lastCalculatedCpu;
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to retrieve CPU usage metrics.");
            return null;
        }
    }

    public CpuMetrics? GetCpuMetrics()
    {
        double? usage = GetCpuUsagePercentage();
        if (!usage.HasValue) return null;

        if (_cachedCpuModel == null)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                _cachedCpuModel = key?.GetValue("ProcessorNameString") as string ?? Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
            }
            catch
            {
                _cachedCpuModel = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "x64 Processor";
            }
        }

        return new CpuMetrics(usage.Value, _cachedCpuModel);
    }

    public MemoryMetrics? GetMemoryMetrics()
    {
        try
        {
            var status = new NativeMethods.MEMORYSTATUSEX();
            if (!NativeMethods.GlobalMemoryStatusEx(ref status))
            {
                return null;
            }

            double totalGb = Math.Round((double)status.ullTotalPhys / (1024 * 1024 * 1024), 1);
            double availGb = Math.Round((double)status.ullAvailPhys / (1024 * 1024 * 1024), 1);
            double usedGb = Math.Round(totalGb - availGb, 1);
            int percent = (int)status.dwMemoryLoad;

            return new MemoryMetrics(usedGb, totalGb, percent);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to retrieve memory metrics.");
            return null;
        }
    }

    public GpuMetrics? GetGpuMetrics()
    {
        try
        {
            // Query DXGI Adapters for real GPU hardware and VRAM info
            var gpu = DxgiGpuReader.GetPrimaryGpuMetrics();
            if (gpu != null)
            {
                return gpu;
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to retrieve DXGI GPU metrics.");
        }

        return new GpuMetrics(
            Name: "GPU Unavailable",
            UsagePercentage: null,
            UsedMemoryGigabytes: null,
            TotalMemoryGigabytes: null,
            IsAvailable: false
        );
    }

    public DiskMetrics? GetDiskMetrics(string? driveLetter = null)
    {
        try
        {
            string root = string.IsNullOrWhiteSpace(driveLetter)
                ? (Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\")
                : driveLetter;

            if (!root.EndsWith('\\')) root += "\\";

            var drive = new DriveInfo(root);
            if (drive.IsReady)
            {
                double totalGb = Math.Round((double)drive.TotalSize / (1024 * 1024 * 1024), 1);
                double freeGb = Math.Round((double)drive.AvailableFreeSpace / (1024 * 1024 * 1024), 1);
                double usedGb = Math.Round(totalGb - freeGb, 1);
                int usedPct = totalGb > 0 ? (int)Math.Round((usedGb / totalGb) * 100) : 0;

                return new DiskMetrics(drive.Name.TrimEnd('\\'), totalGb, freeGb, usedGb, usedPct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to retrieve disk metrics.");
        }

        return null;
    }

    public BatteryMetrics? GetBatteryMetrics()
    {
        try
        {
            if (!NativeMethods.GetSystemPowerStatus(out var powerStatus))
            {
                return null;
            }

            // Desktop PC / No battery
            if (powerStatus.BatteryLifePercent == 255 || powerStatus.BatteryFlag == 128)
            {
                return new BatteryMetrics(
                    Percentage: 100,
                    IsCharging: false,
                    IsOnBattery: false,
                    HasBattery: false,
                    StateDescription: "AC Power"
                );
            }

            int percent = powerStatus.BatteryLifePercent;
            bool isCharging = (powerStatus.ACLineStatus == 1) || (powerStatus.BatteryFlag & 8) != 0;
            bool isOnBattery = powerStatus.ACLineStatus == 0;
            string state = isCharging ? $"{percent}% Charging" : $"{percent}% On battery";

            return new BatteryMetrics(
                Percentage: percent,
                IsCharging: isCharging,
                IsOnBattery: isOnBattery,
                HasBattery: true,
                StateDescription: state
            );
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to retrieve battery metrics.");
            return null;
        }
    }

    public NetworkMetrics? GetNetworkMetrics()
    {
        try
        {
            var profile = Windows.Networking.Connectivity.NetworkInformation.GetInternetConnectionProfile();
            if (profile == null)
            {
                return new NetworkMetrics("Disconnected", false, null);
            }

            var level = profile.GetNetworkConnectivityLevel();
            bool isConnected = level == Windows.Networking.Connectivity.NetworkConnectivityLevel.InternetAccess;

            string type = "Ethernet";
            if (profile.IsWlanConnectionProfile) type = "Wi-Fi";
            else if (profile.IsWwanConnectionProfile) type = "Cellular";

            string name = profile.ProfileName ?? type;
            return new NetworkMetrics(type, isConnected, name);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to retrieve network metrics.");
            bool isAvailable = global::System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
            return new NetworkMetrics(isAvailable ? "Connected" : "Disconnected", isAvailable, null);
        }
    }

    public UptimeMetrics GetUptimeMetrics()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        string formatted = uptime.Days > 0
            ? $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m"
            : $"{uptime.Hours}h {uptime.Minutes}m";

        return new UptimeMetrics(uptime, formatted);
    }

    public SystemMetricsSnapshot GetSystemSnapshot(bool detailed = false)
    {
        return new SystemMetricsSnapshot(
            Cpu: GetCpuMetrics(),
            Memory: GetMemoryMetrics(),
            Gpu: detailed ? GetGpuMetrics() : null,
            Disk: GetDiskMetrics(),
            Battery: GetBatteryMetrics(),
            Network: GetNetworkMetrics(),
            Uptime: GetUptimeMetrics()
        );
    }

    #region DXGI Interop for GPU Metrics

    private static class DxgiGpuReader
    {
        private static readonly Guid IID_IDXGIFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");
        private static readonly Guid IID_IDXGIAdapter3 = new("64596774-5c36-41d3-d00d-146423e6765e");

        [DllImport("dxgi.dll")]
        private static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr ppFactory);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DXGI_ADAPTER_DESC1
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Description;
            public uint VendorId;
            public uint DeviceId;
            public uint SubSysId;
            public uint Revision;
            public UIntPtr DedicatedVideoMemory;
            public UIntPtr DedicatedSystemMemory;
            public UIntPtr SharedSystemMemory;
            public long AdapterLuid;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DXGI_QUERY_VIDEO_MEMORY_INFO
        {
            public ulong Budget;
            public ulong CurrentUsage;
            public ulong AvailableForReservation;
            public ulong CurrentReservation;
        }

        [ComImport]
        [Guid("770aae78-f26f-4dba-a829-253c83d1b387")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDXGIFactory1
        {
            int SetPrivateData(ref Guid Name, uint DataSize, IntPtr pData);
            int SetPrivateDataInterface(ref Guid Name, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
            int GetPrivateData(ref Guid Name, ref uint pDataSize, IntPtr pData);
            int GetParent(ref Guid riid, out IntPtr ppParent);
            [PreserveSig]
            int EnumAdapters(uint Adapter, out IntPtr ppAdapter);
            int MakeWindowAssociation(IntPtr WindowHandle, uint Flags);
            int GetWindowAssociation(out IntPtr pWindowHandle);
            int CreateSwapChain([MarshalAs(UnmanagedType.IUnknown)] object pDevice, IntPtr pDesc, out IntPtr ppSwapChain);
            int CreateSoftwareAdapter(IntPtr Module, out IntPtr ppAdapter);
            [PreserveSig]
            int EnumAdapters1(uint Adapter, out IntPtr ppAdapter);
            [PreserveSig]
            int IsCurrent();
        }

        [ComImport]
        [Guid("29038f61-3839-4626-91fd-086879011a05")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDXGIAdapter1
        {
            int SetPrivateData(ref Guid Name, uint DataSize, IntPtr pData);
            int SetPrivateDataInterface(ref Guid Name, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
            int GetPrivateData(ref Guid Name, ref uint pDataSize, IntPtr pData);
            int GetParent(ref Guid riid, out IntPtr ppParent);
            int EnumOutputs(uint Output, out IntPtr ppOutput);
            int GetDesc(IntPtr pDesc);
            int CheckInterfaceSupport(ref Guid InterfaceName, out long pUMDVersion);
            [PreserveSig]
            int GetDesc1(out DXGI_ADAPTER_DESC1 pDesc);
        }

        [ComImport]
        [Guid("64596774-5c36-41d3-d00d-146423e6765e")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDXGIAdapter3
        {
            // Inherited from IDXGIAdapter1
            int SetPrivateData(ref Guid Name, uint DataSize, IntPtr pData);
            int SetPrivateDataInterface(ref Guid Name, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
            int GetPrivateData(ref Guid Name, ref uint pDataSize, IntPtr pData);
            int GetParent(ref Guid riid, out IntPtr ppParent);
            int EnumOutputs(uint Output, out IntPtr ppOutput);
            int GetDesc(IntPtr pDesc);
            int CheckInterfaceSupport(ref Guid InterfaceName, out long pUMDVersion);
            int GetDesc1(out DXGI_ADAPTER_DESC1 pDesc);
            // Inherited from IDXGIAdapter2
            int GetDesc2(IntPtr pDesc);
            // IDXGIAdapter3
            int RegisterHardwareContentProtectionTeardownStatusEvent(IntPtr hEvent, out uint pdwCookie);
            void UnregisterHardwareContentProtectionTeardownStatus(uint dwCookie);
            [PreserveSig]
            int QueryVideoMemoryInfo(uint NodeIndex, uint MemorySegmentGroup, out DXGI_QUERY_VIDEO_MEMORY_INFO pVideoMemoryInfo);
            int SetVideoMemoryReservation(uint NodeIndex, uint MemorySegmentGroup, ulong Reservation);
            int RegisterVideoMemoryBudgetChangeNotificationEvent(IntPtr hEvent, out uint pdwCookie);
            void UnregisterVideoMemoryBudgetChangeNotification(uint dwCookie);
        }

        public static GpuMetrics? GetPrimaryGpuMetrics()
        {
            Guid factoryIid = IID_IDXGIFactory1;
            int hr = CreateDXGIFactory1(ref factoryIid, out IntPtr pFactory);
            if (hr != 0 || pFactory == IntPtr.Zero) return null;

            IDXGIFactory1? factory = null;
            try
            {
                factory = (IDXGIFactory1)Marshal.GetObjectForIUnknown(pFactory);

                string? bestGpuName = null;
                double? bestTotalVram = null;
                double? bestUsedVram = null;

                for (uint i = 0; ; i++)
                {
                    hr = factory.EnumAdapters1(i, out IntPtr pAdapter);
                    if (hr != 0 || pAdapter == IntPtr.Zero) break;

                    try
                    {
                        var adapter = (IDXGIAdapter1)Marshal.GetObjectForIUnknown(pAdapter);
                        if (adapter.GetDesc1(out var desc) == 0)
                        {
                            // Skip software / Microsoft Basic Render Driver
                            if ((desc.Flags & 2) != 0 || desc.Description.Contains("Basic Render", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            double totalVramGb = Math.Round((double)desc.DedicatedVideoMemory.ToUInt64() / (1024 * 1024 * 1024), 1);
                            double? usedVramGb = null;

                            // Query real-time memory usage if IDXGIAdapter3 is supported
                            Guid adapter3Iid = IID_IDXGIAdapter3;
                            if (Marshal.QueryInterface(pAdapter, in adapter3Iid, out IntPtr pAdapter3) == 0 && pAdapter3 != IntPtr.Zero)
                            {
                                try
                                {
                                    var adapter3 = (IDXGIAdapter3)Marshal.GetObjectForIUnknown(pAdapter3);
                                    if (adapter3.QueryVideoMemoryInfo(0, 0 /* DXGI_MEMORY_SEGMENT_GROUP_LOCAL */, out var memInfo) == 0)
                                    {
                                        usedVramGb = Math.Round((double)memInfo.CurrentUsage / (1024 * 1024 * 1024), 2);
                                    }
                                }
                                finally
                                {
                                    Marshal.Release(pAdapter3);
                                }
                            }

                            // Prefer dedicated GPU with higher VRAM if multiple exist (e.g. discrete RTX 3050 over integrated Iris Xe)
                            if (bestGpuName == null || (totalVramGb > (bestTotalVram ?? 0)))
                            {
                                bestGpuName = desc.Description;
                                bestTotalVram = totalVramGb > 0 ? totalVramGb : null;
                                bestUsedVram = usedVramGb;
                            }
                        }
                    }
                    finally
                    {
                        Marshal.Release(pAdapter);
                    }
                }

                if (bestGpuName != null)
                {
                    return new GpuMetrics(
                        Name: bestGpuName,
                        UsagePercentage: null, // No fabricated GPU percentage
                        UsedMemoryGigabytes: bestUsedVram,
                        TotalMemoryGigabytes: bestTotalVram,
                        IsAvailable: true
                    );
                }
            }
            finally
            {
                Marshal.Release(pFactory);
            }

            return null;
        }
    }

    #endregion
}
