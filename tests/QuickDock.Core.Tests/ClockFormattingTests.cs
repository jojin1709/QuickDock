using QuickDock.Modules.Clock;
using Xunit;

namespace QuickDock.Core.Tests;

public class ClockFormattingTests
{
    [Fact]
    public void FormatTime_24Hour_NoSeconds_FormatsCorrectly()
    {
        var dt = new DateTime(2026, 10, 31, 20, 17, 45);
        string formatted = ClockModule.FormatTime(dt, use24Hour: true, showSeconds: false);

        Assert.Equal("20:17", formatted);
    }

    [Fact]
    public void FormatTime_24Hour_WithSeconds_FormatsCorrectly()
    {
        var dt = new DateTime(2026, 10, 31, 20, 17, 45);
        string formatted = ClockModule.FormatTime(dt, use24Hour: true, showSeconds: true);

        Assert.Equal("20:17:45", formatted);
    }

    [Fact]
    public void FormatTime_12Hour_NoSeconds_FormatsCorrectly()
    {
        var dtEvening = new DateTime(2026, 10, 31, 20, 17, 45);
        string formattedEvening = ClockModule.FormatTime(dtEvening, use24Hour: false, showSeconds: false);
        Assert.Equal("8:17 PM", formattedEvening);

        var dtMorning = new DateTime(2026, 10, 31, 9, 5, 0);
        string formattedMorning = ClockModule.FormatTime(dtMorning, use24Hour: false, showSeconds: false);
        Assert.Equal("9:05 AM", formattedMorning);
    }

    [Fact]
    public void FormatTime_12Hour_WithSeconds_FormatsCorrectly()
    {
        var dt = new DateTime(2026, 10, 31, 20, 17, 45);
        string formatted = ClockModule.FormatTime(dt, use24Hour: false, showSeconds: true);

        Assert.Equal("8:17:45 PM", formatted);
    }

    [Fact]
    public void FormatDate_FormatsFullDayAndMonth()
    {
        var dt = new DateTime(2026, 10, 31, 20, 17, 45);
        string formatted = ClockModule.FormatDate(dt);

        Assert.Equal("Saturday, October 31", formatted);
    }
}

