using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using QuickDock.Core.Interfaces;
using QuickDock.Core.Models;
using QuickDock.Core.Settings;
using QuickDock.Infrastructure.Storage;

namespace QuickDock.Services.Weather;

public sealed class OpenMeteoWeatherProvider : IWeatherProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenMeteoWeatherProvider> _logger;

    public string Name => "OpenMeteo";

    public OpenMeteoWeatherProvider(HttpClient httpClient, ILogger<OpenMeteoWeatherProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(double Latitude, double Longitude, string DisplayName)?> ResolveLocationAsync(string locationQuery, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(locationQuery)) return null;

        try
        {
            var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(locationQuery.Trim())}&count=1&language=en&format=json";
            var response = await _httpClient.GetFromJsonAsync<GeocodingResponse>(url, cancellationToken).ConfigureAwait(false);
            if (response?.Results != null && response.Results.Count > 0)
            {
                var first = response.Results[0];
                var name = string.IsNullOrWhiteSpace(first.Country) ? first.Name : $"{first.Name}, {first.Country}";
                return (first.Latitude, first.Longitude, name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve geocoding location for {Query}", locationQuery);
        }

        return null;
    }

    public async Task<WeatherData?> FetchWeatherAsync(string locationName, double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,relative_humidity_2m,apparent_temperature,weather_code,wind_speed_10m";
            var response = await _httpClient.GetFromJsonAsync<OpenMeteoForecastResponse>(url, cancellationToken).ConfigureAwait(false);
            if (response?.Current != null)
            {
                return new WeatherData
                {
                    LocationName = locationName,
                    TemperatureC = Math.Round(response.Current.Temperature2m, 1),
                    ApparentTemperatureC = Math.Round(response.Current.ApparentTemperature, 1),
                    RelativeHumidity = response.Current.RelativeHumidity2m,
                    WindSpeedKmh = Math.Round(response.Current.WindSpeed10m, 1),
                    WeatherCode = response.Current.WeatherCode,
                    Condition = MapWeatherCodeToCondition(response.Current.WeatherCode),
                    FetchedAt = DateTime.UtcNow,
                    IsCached = false
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch weather from Open-Meteo for {Location}", locationName);
        }

        return null;
    }

    public static string MapWeatherCodeToCondition(int code) => code switch
    {
        0 => "Clear sky",
        1 => "Mainly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 => "Fog",
        48 => "Depositing rime fog",
        51 => "Light drizzle",
        53 => "Moderate drizzle",
        55 => "Dense drizzle",
        61 => "Slight rain",
        63 => "Moderate rain",
        65 => "Heavy rain",
        71 => "Slight snow fall",
        73 => "Moderate snow fall",
        75 => "Heavy snow fall",
        77 => "Snow grains",
        80 => "Slight rain showers",
        81 => "Moderate rain showers",
        82 => "Violent rain showers",
        85 => "Slight snow showers",
        86 => "Heavy snow showers",
        95 => "Thunderstorm",
        96 or 99 => "Thunderstorm with hail",
        _ => "Fair"
    };

    private sealed class GeocodingResponse
    {
        [JsonPropertyName("results")]
        public List<GeocodingResult>? Results { get; set; }
    }

    private sealed class GeocodingResult
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; } = string.Empty;
    }

    private sealed class OpenMeteoForecastResponse
    {
        [JsonPropertyName("current")]
        public CurrentWeatherDto? Current { get; set; }
    }

    private sealed class CurrentWeatherDto
    {
        [JsonPropertyName("temperature_2m")]
        public double Temperature2m { get; set; }

        [JsonPropertyName("apparent_temperature")]
        public double ApparentTemperature { get; set; }

        [JsonPropertyName("relative_humidity_2m")]
        public int RelativeHumidity2m { get; set; }

        [JsonPropertyName("wind_speed_10m")]
        public double WindSpeed10m { get; set; }

        [JsonPropertyName("weather_code")]
        public int WeatherCode { get; set; }
    }
}

public sealed class WeatherService : IWeatherService
{
    private readonly IWeatherProvider _provider;
    private readonly ISettingsManager _settingsManager;
    private readonly ILogger<WeatherService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private WeatherData? _cachedWeather;
    private readonly string _cacheFilePath;

    public event EventHandler<WeatherData?>? WeatherUpdated;

    public WeatherData? CurrentWeather => _cachedWeather;

    public WeatherService(
        IWeatherProvider provider,
        ISettingsManager settingsManager,
        ILogger<WeatherService> logger)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        AppStoragePaths.EnsureDirectoriesExist();
        _cacheFilePath = Path.Combine(AppStoragePaths.CacheDirectory, "weather_cache.json");
        LoadCacheFromDisk();
    }

    private void LoadCacheFromDisk()
    {
        try
        {
            if (File.Exists(_cacheFilePath))
            {
                var json = File.ReadAllText(_cacheFilePath);
                var data = JsonSerializer.Deserialize<WeatherData>(json);
                if (data != null)
                {
                    data.IsCached = true;
                    _cachedWeather = data;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read cached weather file.");
        }
    }

    private void SaveCacheToDisk(WeatherData data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_cacheFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist weather cache to disk.");
        }
    }

    public async Task<WeatherData?> GetWeatherAsync(bool forceRefresh = false)
    {
        var settings = _settingsManager.CurrentSettings.Weather;
        var maxAge = TimeSpan.FromMinutes(Math.Max(5, settings.RefreshIntervalMinutes));

        // Use cache if still valid and not forcing refresh
        if (!forceRefresh && _cachedWeather != null && (DateTime.UtcNow - _cachedWeather.FetchedAt) < maxAge)
        {
            return _cachedWeather;
        }

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Double check inside lock
            if (!forceRefresh && _cachedWeather != null && (DateTime.UtcNow - _cachedWeather.FetchedAt) < maxAge)
            {
                return _cachedWeather;
            }

            double lat = settings.ManualLatitude ?? 10.5276;
            double lon = settings.ManualLongitude ?? 76.2144;
            string locationName = settings.ManualLocationName ?? "Thrissur";

            if (settings.UseAutomaticLocation || settings.ManualLatitude == null)
            {
                var resolved = await _provider.ResolveLocationAsync(locationName).ConfigureAwait(false);
                if (resolved.HasValue)
                {
                    lat = resolved.Value.Latitude;
                    lon = resolved.Value.Longitude;
                    locationName = resolved.Value.DisplayName;
                }
            }

            var freshData = await _provider.FetchWeatherAsync(locationName, lat, lon).ConfigureAwait(false);
            if (freshData != null)
            {
                _cachedWeather = freshData;
                SaveCacheToDisk(freshData);
                WeatherUpdated?.Invoke(this, freshData);
                return freshData;
            }

            // Fallback to stale cache if network failed
            if (_cachedWeather != null)
            {
                _cachedWeather.IsCached = true;
                return _cachedWeather;
            }

            return null;
        }
        finally
        {
            _lock.Release();
        }
    }
}

