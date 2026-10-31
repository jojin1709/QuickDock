using QuickDock.Infrastructure.Windows;
using Xunit;

namespace QuickDock.Core.Tests;

public class FullscreenDetectionTests
{
    [Fact]
    public void EvaluateFullscreenBounds_ExactMatch_ReturnsTrue()
    {
        // Window bounds match monitor bounds exactly (1920x1080)
        bool result = FullscreenDetectionService.EvaluateFullscreenBounds(
            winLeft: 0, winTop: 0, winRight: 1920, winBottom: 1080,
            monLeft: 0, monTop: 0, monRight: 1920, monBottom: 1080
        );

        Assert.True(result);
    }

    [Fact]
    public void EvaluateFullscreenBounds_SpanningBeyondMonitor_ReturnsTrue()
    {
        // Borderless game window slightly exceeding monitor bounds
        bool result = FullscreenDetectionService.EvaluateFullscreenBounds(
            winLeft: -2, winTop: -2, winRight: 1922, winBottom: 1082,
            monLeft: 0, monTop: 0, monRight: 1920, monBottom: 1080
        );

        Assert.True(result);
    }

    [Fact]
    public void EvaluateFullscreenBounds_MaximizedWithTaskbar_ReturnsFalse()
    {
        // Normal maximized window that leaves 48px taskbar at bottom
        bool result = FullscreenDetectionService.EvaluateFullscreenBounds(
            winLeft: 0, winTop: 0, winRight: 1920, winBottom: 1032,
            monLeft: 0, monTop: 0, monRight: 1920, monBottom: 1080
        );

        Assert.False(result);
    }

    [Fact]
    public void EvaluateFullscreenBounds_SecondaryMonitor_ExactMatch_ReturnsTrue()
    {
        // Secondary monitor placed at (1920, 0)
        bool result = FullscreenDetectionService.EvaluateFullscreenBounds(
            winLeft: 1920, winTop: 0, winRight: 3840, winBottom: 1080,
            monLeft: 1920, monTop: 0, monRight: 3840, monBottom: 1080
        );

        Assert.True(result);
    }

    [Fact]
    public void EvaluateFullscreenBounds_WindowOnOtherMonitor_ReturnsFalse()
    {
        // Window on primary monitor tested against secondary monitor
        bool result = FullscreenDetectionService.EvaluateFullscreenBounds(
            winLeft: 0, winTop: 0, winRight: 1920, winBottom: 1080,
            monLeft: 1920, monTop: 0, monRight: 3840, monBottom: 1080
        );

        Assert.False(result);
    }
}
