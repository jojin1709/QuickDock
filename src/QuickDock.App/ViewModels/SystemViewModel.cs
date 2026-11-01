using System.Net.NetworkInformation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using QuickDock.Core.Interfaces;
using QuickDock.Services.System;

namespace QuickDock.App.ViewModels;

public sealed partial class SystemViewModel : ObservableObject
{
    private readonly ISystemMetricsService _metricsService;
    private readonly IAudioService _audioService;
    private readonly ILogger<SystemViewModel> _logger;
    private DispatcherQueueTimer? _refreshTimer;
    private DispatcherQueue? _dispatcherQueue;
    private bool _isInternalVolumeUpdate;
    private long _lastBytesReceived;
    private long _lastBytesSent;
    private DateTime _lastSpeedCheck = DateTime.UtcNow;

    [ObservableProperty]
    private string _cpuUsageText = "0%";

    [ObservableProperty]
    private string _cpuModelText = "Processor";

    [ObservableProperty]
    private double _cpuUsageValue = 0;

    [ObservableProperty]
    private string _memoryText = "0 / 0 GB";

    [ObservableProperty]
    private string _memoryPercentageText = "0%";

    [ObservableProperty]
    private int _memoryPercentage = 0;

    [ObservableProperty]
    private string _gpuNameText = "GPU Unavailable";

    [ObservableProperty]
    private string _gpuMemoryText = "—";

    [ObservableProperty]
    private bool _gpuIsAvailable = false;

    [ObservableProperty]
    private string _diskNameText = "C:";

    [ObservableProperty]
    private string _diskSpaceText = "— free";

    [ObservableProperty]
    private string _diskUsagePercentageText = "0% used";

    [ObservableProperty]
    private int _diskUsagePercentage = 0;

    [ObservableProperty]
    private string _batteryText = "AC Power";

    [ObservableProperty]
    private string _networkTypeText = "Network";

    [ObservableProperty]
    private string _networkStatusText = "Checking...";

    [ObservableProperty]
    private string _uptimeText = "—";

    [ObservableProperty]
    private string _batteryPercentText = "80%";

    [ObservableProperty]
    private string _batteryStateText = "Plugged in";

    [ObservableProperty]
    private string _batteryHealthText = "Health 92%";

    [ObservableProperty]
    private double _batteryPercentValue = 80;

    [ObservableProperty]
    private string _networkSsidText = "D-Link";

    [ObservableProperty]
    private string _networkSpeedText = "Wi-Fi 4 · 162 Mbps";

    [ObservableProperty]
    private string _networkDownSpeedText = "5 KB/s";

    [ObservableProperty]
    private string _networkUpSpeedText = "128 KB/s";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumePercentText))]
    private int _volumePercent = 32;

    public string VolumePercentText => $"{VolumePercent}%";

    [ObservableProperty]
    private string _volumeDeviceName = "Realtek(R) Audio";

    public SystemViewModel(ISystemMetricsService metricsService, IAudioService audioService, ILogger<SystemViewModel> logger)
    {
        _metricsService = metricsService;
        _audioService = audioService;
        _logger = logger;

        _audioService.VolumeChanged += (s, e) =>
        {
            _dispatcherQueue?.TryEnqueue(() =>
            {
                _isInternalVolumeUpdate = true;
                try
                {
                    VolumePercent = (int)Math.Round(e.NewVolume * 100);
                    VolumeDeviceName = _audioService.GetDefaultDeviceName();
                }
                finally
                {
                    _isInternalVolumeUpdate = false;
                }
            });
        };
    }

    partial void OnVolumePercentChanged(int value)
    {
        if (_isInternalVolumeUpdate) return;
        _audioService.SetMasterVolume(Math.Clamp(value / 100f, 0f, 1f));
    }

    private static string FormatThroughput(double bytesPerSec) => bytesPerSec switch
    {
        >= 1_048_576 => $"{(bytesPerSec / 1_048_576):0.#} MB/s",
        >= 1024 => $"{(bytesPerSec / 1024):0.#} KB/s",
        _ => $"{(int)bytesPerSec} B/s"
    };

    public void Refresh()
    {
        var snapshot = _metricsService.GetSystemSnapshot(detailed: true);

        if (snapshot.Cpu != null)
        {
            CpuUsageValue = snapshot.Cpu.UsagePercentage;
            CpuUsageText = $"{snapshot.Cpu.UsagePercentage:0.#}%";
            CpuModelText = snapshot.Cpu.ModelName ?? "x64 Processor";
        }

        if (snapshot.Memory != null)
        {
            MemoryText = $"{snapshot.Memory.UsedGigabytes:0.#} / {snapshot.Memory.TotalGigabytes:0.#} GB";
            MemoryPercentage = snapshot.Memory.Percentage;
            MemoryPercentageText = $"{snapshot.Memory.Percentage}%";
        }

        if (snapshot.Gpu != null && snapshot.Gpu.IsAvailable)
        {
            GpuIsAvailable = true;
            GpuNameText = snapshot.Gpu.Name;
            if (snapshot.Gpu.UsedMemoryGigabytes.HasValue && snapshot.Gpu.TotalMemoryGigabytes.HasValue)
            {
                GpuMemoryText = $"{snapshot.Gpu.UsedMemoryGigabytes.Value:0.##} / {snapshot.Gpu.TotalMemoryGigabytes.Value:0.#} GB";
            }
            else if (snapshot.Gpu.TotalMemoryGigabytes.HasValue)
            {
                GpuMemoryText = $"{snapshot.Gpu.TotalMemoryGigabytes.Value:0.#} GB VRAM";
            }
            else
            {
                GpuMemoryText = "Active";
            }
        }
        else
        {
            GpuIsAvailable = false;
            GpuNameText = "GPU Unavailable";
            GpuMemoryText = "—";
        }

        if (snapshot.Disk != null)
        {
            DiskNameText = $"{snapshot.Disk.DriveLetter} Drive";
            DiskSpaceText = $"{snapshot.Disk.FreeGigabytes:0.#} GB free of {snapshot.Disk.TotalGigabytes:0.#} GB";
            DiskUsagePercentage = snapshot.Disk.UsedPercentage;
            DiskUsagePercentageText = $"{snapshot.Disk.UsedPercentage}% used";
        }

        if (snapshot.Battery != null && snapshot.Battery.HasBattery)
        {
            BatteryPercentValue = snapshot.Battery.Percentage;
            BatteryPercentText = $"{snapshot.Battery.Percentage}%";
            BatteryStateText = snapshot.Battery.IsCharging ? "Charging" : (snapshot.Battery.IsOnBattery ? "On Battery" : "Plugged In");
            BatteryHealthText = snapshot.Battery.StateDescription;
            BatteryText = snapshot.Battery.StateDescription;
        }
        else
        {
            BatteryPercentValue = 100;
            BatteryPercentText = "AC Power";
            BatteryStateText = "Wall Power";
            BatteryHealthText = "Desktop (No Battery)";
            BatteryText = "AC Power";
        }

        if (snapshot.Network != null)
        {
            NetworkTypeText = snapshot.Network.InterfaceType;
            NetworkStatusText = snapshot.Network.IsConnected ? "Connected" : "Disconnected";
            NetworkSsidText = string.IsNullOrEmpty(snapshot.Network.ConnectionName) ? snapshot.Network.InterfaceType : snapshot.Network.ConnectionName;

            try
            {
                var now = DateTime.UtcNow;
                double elapsedSec = (now - _lastSpeedCheck).TotalSeconds;
                if (elapsedSec > 0.5)
                {
                    long totalRx = 0;
                    long totalTx = 0;
                    long maxLinkSpeed = 0;
                    foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (ni.OperationalStatus == OperationalStatus.Up &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                            ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                        {
                            var stats = ni.GetIPStatistics();
                            totalRx += stats.BytesReceived;
                            totalTx += stats.BytesSent;
                            if (ni.Speed > maxLinkSpeed) maxLinkSpeed = ni.Speed;
                        }
                    }

                    if (_lastBytesReceived > 0 && totalRx >= _lastBytesReceived)
                    {
                        double rxSpeed = (totalRx - _lastBytesReceived) / elapsedSec;
                        double txSpeed = (totalTx - _lastBytesSent) / elapsedSec;
                        NetworkDownSpeedText = FormatThroughput(rxSpeed);
                        NetworkUpSpeedText = FormatThroughput(txSpeed);
                    }

                    if (maxLinkSpeed > 0)
                    {
                        NetworkSpeedText = $"{snapshot.Network.InterfaceType} · {maxLinkSpeed / 1_000_000} Mbps";
                    }

                    _lastBytesReceived = totalRx;
                    _lastBytesSent = totalTx;
                    _lastSpeedCheck = now;
                }
            }
            catch { }
        }

        // Live Audio Volume
        try
        {
            float vol = _audioService.GetMasterVolume();
            _isInternalVolumeUpdate = true;
            try
            {
                VolumePercent = (int)Math.Round(vol * 100);
                VolumeDeviceName = _audioService.GetDefaultDeviceName();
            }
            finally
            {
                _isInternalVolumeUpdate = false;
            }
        }
        catch { }

        if (snapshot.Uptime != null)
        {
            UptimeText = snapshot.Uptime.Formatted;
        }
    }

    public void StartLiveUpdates(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
        Refresh();

        if (_refreshTimer == null)
        {
            _refreshTimer = dispatcherQueue.CreateTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(2);
            _refreshTimer.Tick += (s, e) => Refresh();
        }

        _refreshTimer.Start();
        _logger.LogInformation("SystemViewModel live monitoring started.");
    }

    public void StopLiveUpdates()
    {
        if (_refreshTimer != null)
        {
            _refreshTimer.Stop();
            _refreshTimer = null;
            _logger.LogInformation("SystemViewModel live monitoring stopped.");
        }
    }
}

