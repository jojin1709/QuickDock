using Microsoft.Extensions.Logging;
using QuickDock.Infrastructure.Storage;
using Serilog;
using Serilog.Events;

namespace QuickDock.Infrastructure.Logging;

/// <summary>
/// Configures privacy-first structured logging for QuickDock.
/// </summary>
public static class QuickDockLogging
{
    public static void InitializeSerilog()
    {
        AppStoragePaths.EnsureDirectoriesExist();

        var logPath = Path.Combine(AppStoragePaths.LogsDirectory, "quickdock-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    public static void ConfigureLogging(ILoggingBuilder builder)
    {
        InitializeSerilog();
        builder.ClearProviders();
        builder.AddSerilog(dispose: true);
    }
}
