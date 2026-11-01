using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Core.Settings;

namespace QuickDock.App.ViewModels;

public sealed partial class WeatherViewModel : ObservableObject
{
    private readonly IWeatherService _weatherService;
    private readonly ISettingsManager _settingsManager;
    private readonly DispatcherQueue? _dispatcherQueue;

    [ObservableProperty]
    private string _locationName = "Loading...";

    [ObservableProperty]
    private string _temperatureText = "--°";

    [ObservableProperty]
    private string _conditionText = "Checking...";

    [ObservableProperty]
    private string _feelsLikeText = "Feels like --°";

    [ObservableProperty]
    private string _humidityText = "Humidity --%";

    [ObservableProperty]
    private string _windText = "Wind -- km/h";

    [ObservableProperty]
    private string _cacheBadgeText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _manualCityInput = string.Empty;

    public WeatherViewModel(
        IWeatherService weatherService,
        ISettingsManager settingsManager,
        DispatcherQueue? dispatcherQueue = null)
    {
        _weatherService = weatherService ?? throw new ArgumentNullException(nameof(weatherService));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread();

        ManualCityInput = _settingsManager.CurrentSettings.Weather.ManualLocationName ?? string.Empty;
        _weatherService.WeatherUpdated += OnWeatherUpdated;
    }

    private void Dispatch(Action action)
    {
        if (_dispatcherQueue != null)
        {
            _dispatcherQueue.TryEnqueue(() => action());
        }
        else
        {
            action();
        }
    }

    public async Task RefreshWeatherAsync(bool force = false)
    {
        StatusMessage = "Fetching weather...";
        try
        {
            var data = await _weatherService.GetWeatherAsync(forceRefresh: force);
            Dispatch(() =>
            {
                UpdateUI(data);
            });
        }
        catch
        {
            Dispatch(() =>
            {
                StatusMessage = "Weather unavailable";
            });
        }
    }

    [RelayCommand]
    private async Task UpdateManualLocationAsync()
    {
        if (string.IsNullOrWhiteSpace(ManualCityInput)) return;

        var s = _settingsManager.CurrentSettings;
        s.Weather.ManualLocationName = ManualCityInput.Trim();
        s.Weather.ManualLatitude = null; // Forces re-geocoding
        await _settingsManager.SaveAsync(s);
        await RefreshWeatherAsync(force: true);
    }

    private void OnWeatherUpdated(object? sender, WeatherData? data)
    {
        Dispatch(() =>
        {
            UpdateUI(data);
        });
    }

    private void UpdateUI(WeatherData? data)
    {
        if (data == null)
        {
            StatusMessage = "Weather unavailable";
            ConditionText = "Unavailable";
            return;
        }

        LocationName = data.LocationName;
        TemperatureText = $"{Math.Round(data.TemperatureC)}°C";
        ConditionText = data.Condition;
        FeelsLikeText = $"Feels like {Math.Round(data.ApparentTemperatureC)}°C";
        HumidityText = $"Humidity {data.RelativeHumidity}%";
        WindText = $"Wind {data.WindSpeedKmh} km/h";
        CacheBadgeText = data.IsCached ? "Cached" : "Live";
        StatusMessage = $"Updated at {data.FetchedAt.ToLocalTime():HH:mm}";
    }
}
