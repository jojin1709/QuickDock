using Microsoft.Extensions.Logging;
using QuickDock.Core.Settings;
using QuickDock.Infrastructure.Storage;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace QuickDock.Infrastructure.Logging;

/// <summary>
/// Configures privacy-first structured logging for QuickDock.
/// </summary>
public static class QuickDockLogging
{
    public static readonly LoggingLevelSwitch LevelSwitch = new(LogEventLevel.Information);

    public static void InitializeSerilog()
    {
        AppStoragePaths.EnsureDirectoriesExist();

        var logPath = Path.Combine(AppStoragePaths.LogsDirectory, "quickdock-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(LevelSwitch)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                buffered: false,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    public static void SetLoggingLevel(string? level)
    {
        LevelSwitch.MinimumLevel = (level?.Trim().ToLowerInvariant()) switch
        {
            "debug" or "verbose" => LogEventLevel.Debug,
            "warning" or "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            _ => LogEventLevel.Information
        };
    }

    public static void ConfigureLogging(ILoggingBuilder builder)
    {
        InitializeSerilog();
        builder.ClearProviders();
        builder.AddSerilog(dispose: true);
    }
}

