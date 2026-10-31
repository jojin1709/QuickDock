using System;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;

namespace QuickDock.Services.Developer;

public sealed class TimestampToolkitService : ITimestampToolkitService
{
    public TimestampConversionResult ConvertFromUnixSeconds(long seconds)
    {
        try
        {
            var dto = DateTimeOffset.FromUnixTimeSeconds(seconds);
            return BuildResult(dto);
        }
        catch
        {
            return BuildResult(DateTimeOffset.UtcNow);
        }
    }

    public TimestampConversionResult ConvertFromUnixMilliseconds(long milliseconds)
    {
        try
        {
            var dto = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
            return BuildResult(dto);
        }
        catch
        {
            return BuildResult(DateTimeOffset.UtcNow);
        }
    }

    public TimestampConversionResult ConvertFromDateTime(DateTimeOffset dateTime)
    {
        return BuildResult(dateTime);
    }

    public TimestampConversionResult GetCurrent()
    {
        return BuildResult(DateTimeOffset.UtcNow);
    }

    private static TimestampConversionResult BuildResult(DateTimeOffset utc)
    {
        var local = utc.ToLocalTime();
        long sec = utc.ToUnixTimeSeconds();
        long ms = utc.ToUnixTimeMilliseconds();

        return new TimestampConversionResult
        {
            UnixSeconds = sec,
            UnixMilliseconds = ms,
            UtcDateTime = utc,
            LocalDateTime = local,
            FormattedIso = local.ToString("yyyy-MM-dd HH:mm:ss zzz"),
            RelativeDescription = FormatRelativeTime(utc)
        };
    }

    private static string FormatRelativeTime(DateTimeOffset target)
    {
        var diff = DateTimeOffset.UtcNow - target;
        bool isPast = diff.TotalSeconds >= 0;
        var abs = diff.Duration();

        string unit;
        if (abs.TotalSeconds < 60)
            unit = $"{(int)abs.TotalSeconds} seconds";
        else if (abs.TotalMinutes < 60)
            unit = $"{(int)abs.TotalMinutes} minutes";
        else if (abs.TotalHours < 24)
            unit = $"{(int)abs.TotalHours} hours";
        else
            unit = $"{(int)abs.TotalDays} days";

        return isPast ? $"{unit} ago" : $"in {unit}";
    }
}

