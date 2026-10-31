using QuickDock.Infrastructure.Windows;
using Xunit;

namespace QuickDock.Core.Tests;

public class StartupServiceTests
{
    [Fact]
    public void GetStartupCommandLine_WrapsPathAndAppendsSilentFlag()
    {
        string exe = @"C:\Program Files\QuickDock\QuickDock.App.exe";
        string expected = "\"C:\\Program Files\\QuickDock\\QuickDock.App.exe\" --silent";

        string actual = StartupService.GetStartupCommandLine(exe);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetStartupCommandLine_WithSpacesInPath_HandlesQuotesCorrectly()
    {
        string exe = @"D:\Projects\QuickDock Binaries\QuickDock.App.exe";
        string actual = StartupService.GetStartupCommandLine(exe);

        Assert.StartsWith("\"", actual);
        Assert.EndsWith("\" --silent", actual);
    }
}
