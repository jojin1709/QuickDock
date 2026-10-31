using Microsoft.Extensions.Logging.Abstractions;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Infrastructure.Storage;
using QuickDock.Services.Weather;
using Xunit;

namespace QuickDock.Core.Tests;

public class MockWeatherProvider : IWeatherProvider
{
    public string Name => "MockProvider";
    public WeatherData? ReturnData { get; set; }
    public bool ShouldFail { get; set; }

    public Task<WeatherData?> FetchWeatherAsync(string locationName, double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        if (ShouldFail) return Task.FromResult<WeatherData?>(null);
        var data = ReturnData ?? new WeatherData
        {
            LocationName = locationName,
            TemperatureC = 28.5,
            ApparentTemperatureC = 31.0,
            RelativeHumidity = 70,
            WindSpeedKmh = 12.0,
            Condition = "Partly cloudy",
            FetchedAt = DateTime.UtcNow
        };
        return Task.FromResult<WeatherData?>(data);
    }

    public Task<(double Latitude, double Longitude, string DisplayName)?> ResolveLocationAsync(string locationQuery, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<(double Latitude, double Longitude, string DisplayName)?>((10.5276, 76.2144, locationQuery));
    }
}

public class WeatherAndScreenTimeTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteDatabaseService _db;

    public WeatherAndScreenTimeTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"quickdock_test_ws_{Guid.NewGuid():N}.db");
        _db = new SqliteDatabaseService(NullLogger<SqliteDatabaseService>.Instance, _testDbPath);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Theory]
    [InlineData(0, "Clear sky")]
    [InlineData(2, "Partly cloudy")]
    [InlineData(61, "Slight rain")]
    [InlineData(71, "Slight snow fall")]
    [InlineData(95, "Thunderstorm")]
    public void OpenMeteoWeatherProvider_MapsWeatherCodesToConditions(int code, string expected)
    {
        var condition = OpenMeteoWeatherProvider.MapWeatherCodeToCondition(code);
        Assert.Equal(expected, condition);
    }

    [Fact]
    public async Task WeatherService_CachesData_AndReusesWithoutNetwork()
    {
        var mockProvider = new MockWeatherProvider();
        var settings = new MockSettingsManager();
        settings.CurrentSettings.Weather.RefreshIntervalMinutes = 30;

        var weatherService = new WeatherService(mockProvider, settings, NullLogger<WeatherService>.Instance);

        // First fetch calls provider
        var data1 = await weatherService.GetWeatherAsync(forceRefresh: true);
        Assert.NotNull(data1);
        Assert.Equal(28.5, data1.TemperatureC);

        // Make provider fail; subsequent fetch should return cached data
        mockProvider.ShouldFail = true;
        var data2 = await weatherService.GetWeatherAsync(forceRefresh: false);
        Assert.NotNull(data2);
        Assert.Equal(28.5, data2.TemperatureC);
    }

    [Fact]
    public async Task ScreenTime_Aggregation_DailyAndWeekly_Works()
    {
        await _db.InitializeAsync();

        // Simulate recording usage for apps
        await _db.RecordAppUsageAsync("devenv", "QuickDock - Microsoft Visual Studio", 120);
        await _db.RecordAppUsageAsync("devenv", "QuickDock - Microsoft Visual Studio", 60); // Aggregates: 180s
        await _db.RecordAppUsageAsync("chrome", "GitHub - jojin1709/QuickDock", 300);

        var daily = await _db.GetDailyUsageAsync(DateTime.Today);
        Assert.Equal(2, daily.Count);

        var vsUsage = daily.First(d => d.AppName == "devenv");
        Assert.Equal(180, vsUsage.DurationSeconds);

        var chromeUsage = daily.First(d => d.AppName == "chrome");
        Assert.Equal(300, chromeUsage.DurationSeconds);

        // Weekly
        var weekly = await _db.GetWeeklyUsageAsync(DateTime.Today.AddDays(-7), DateTime.Today);
        Assert.Equal(2, weekly.Count);
        Assert.Equal(480, weekly.Sum(w => w.DurationSeconds));

        // Clear
        await _db.ClearUsageHistoryAsync();
        var cleared = await _db.GetDailyUsageAsync(DateTime.Today);
        Assert.Empty(cleared);
    }
}
