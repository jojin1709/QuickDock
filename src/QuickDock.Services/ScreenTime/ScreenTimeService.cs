using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Core.Settings;

namespace QuickDock.Services.ScreenTime;

public sealed class ScreenTimeService : IScreenTimeService
{
    private readonly IDatabaseService _databaseService;
    private readonly ISettingsManager _settingsManager;
    private readonly ILogger<ScreenTimeService> _logger;
    private readonly object _lock = new();

    private global::System.Threading.Timer? _samplingTimer;
    private string _currentAppName = string.Empty;
    private string _currentWindowTitle = string.Empty;
    private DateTime _lastSampleTime = DateTime.UtcNow;
    private bool _isTracking;

    public bool IsTracking => _isTracking;

    public ScreenTimeService(
        IDatabaseService databaseService,
        ISettingsManager settingsManager,
        ILogger<ScreenTimeService> logger)
    {
        _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void StartTracking()
    {
        lock (_lock)
        {
            if (_isTracking || !_settingsManager.CurrentSettings.ScreenTime.TrackingEnabled) return;

            _lastSampleTime = DateTime.UtcNow;
            _samplingTimer = new global::System.Threading.Timer(OnSampleForegroundWindow, null, 2000, 2000);
            _isTracking = true;
            _logger.LogInformation("ScreenTime tracking started.");
        }
    }

    public void StopTracking()
    {
        lock (_lock)
        {
            if (!_isTracking) return;

            FlushCurrentDuration();
            _samplingTimer?.Dispose();
            _samplingTimer = null;
            _isTracking = false;
            _logger.LogInformation("ScreenTime tracking stopped.");
        }
    }

    private void OnSampleForegroundWindow(object? state)
    {
        if (!_settingsManager.CurrentSettings.ScreenTime.TrackingEnabled)
        {
            StopTracking();
            return;
        }

        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            GetWindowThreadProcessId(hwnd, out var processId);
            if (processId == 0) return;

            string appName;
            try
            {
                using var proc = Process.GetProcessById((int)processId);
                appName = proc.ProcessName;
            }
            catch
            {
                return;
            }

            var titleSb = new StringBuilder(256);
            GetWindowTextW(hwnd, titleSb, titleSb.Capacity);
            var title = titleSb.ToString();

            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var elapsedSeconds = (long)(now - _lastSampleTime).TotalSeconds;
                _lastSampleTime = now;

                if (!string.IsNullOrEmpty(_currentAppName) && elapsedSeconds > 0 && elapsedSeconds < 60)
                {
                    _ = _databaseService.RecordAppUsageAsync(_currentAppName, _currentWindowTitle, elapsedSeconds);
                }

                _currentAppName = appName;
                _currentWindowTitle = title;
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Foreground window sampling error.");
        }
    }

    private void FlushCurrentDuration()
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = (long)(now - _lastSampleTime).TotalSeconds;
        _lastSampleTime = now;

        if (!string.IsNullOrEmpty(_currentAppName) && elapsedSeconds > 0 && elapsedSeconds < 60)
        {
            _ = _databaseService.RecordAppUsageAsync(_currentAppName, _currentWindowTitle, elapsedSeconds);
        }
    }

    public Task<List<AppUsageRecord>> GetDailyUsageAsync(DateTime date)
    {
        FlushCurrentDuration();
        return _databaseService.GetDailyUsageAsync(date);
    }

    public Task<List<AppUsageRecord>> GetWeeklyUsageAsync(DateTime startDate, DateTime endDate)
    {
        FlushCurrentDuration();
        return _databaseService.GetWeeklyUsageAsync(startDate, endDate);
    }

    public Task ClearHistoryAsync()
    {
        return _databaseService.ClearUsageHistoryAsync();
    }

    public void Dispose()
    {
        StopTracking();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
}
