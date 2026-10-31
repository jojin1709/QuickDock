using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using QuickDock.Core.Interfaces;

namespace QuickDock.Infrastructure.Windows;

public sealed class StartupService : IStartupService
{
    private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppValueName = "QuickDock";
    private readonly ILogger<StartupService> _logger;

    public StartupService(ILogger<StartupService> logger)
    {
        _logger = logger;
    }

    public static string GetStartupCommandLine(string executablePath)
    {
        return $"\"{executablePath}\" --silent";
    }

    public bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: false);
            if (key == null) return false;

            object? val = key.GetValue(AppValueName);
            if (val is string str && !string.IsNullOrWhiteSpace(str))
            {
                string? exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe) && str.Contains(exe, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check startup registry status.");
            return false;
        }
    }

    public bool SetStartupEnabled(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, writable: true);
            if (key == null)
            {
                _logger.LogWarning("Run registry key could not be opened for writing.");
                return false;
            }

            if (enable)
            {
                string? exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe))
                {
                    _logger.LogWarning("Environment.ProcessPath is null; cannot configure startup.");
                    return false;
                }

                string cmd = GetStartupCommandLine(exe);
                key.SetValue(AppValueName, cmd, RegistryValueKind.String);
                _logger.LogInformation("QuickDock registered to start with Windows: {Command}", cmd);
            }
            else
            {
                if (key.GetValue(AppValueName) != null)
                {
                    key.DeleteValue(AppValueName, throwOnMissingValue: false);
                    _logger.LogInformation("QuickDock unregistered from Windows startup.");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update startup registry setting (enable={Enable}).", enable);
            return false;
        }
    }
}

