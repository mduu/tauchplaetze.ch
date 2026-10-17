using System.Globalization;
using System.Text.Json;

namespace Tauchplaetze.Spike.DataSources.Weather;

/// <summary>
/// Typed client for weather models through Open-Meteo (<c>https://api.open-meteo.com</c>).
/// </summary>
/// <remarks>
/// The Alplakes API has no MeteoSwiss proxy any more. MeteoSwiss publishes ICON-CH1/CH2 as open data,
/// but only as GRIB2 files. Open-Meteo decodes these files and serves JSON per point.
/// </remarks>
internal class OpenMeteoClient(HttpClient httpClient, RawResponseStore store)
{
    /// <summary>
    /// Gets the hourly weather of one model. MeteoSwiss ICON-CH1 has 1 km and 33 hours lead time, ICON-CH2 2 km and 5 days.
    /// DWD ICON and ECMWF IFS are coarser, but reach 7 and 15 days.
    /// </summary>
    public async Task<(SourceResponse Response, HourlyWeather? Weather)> GetHourly(
        string model, double latitude, double longitude, int pastDays, int forecastDays, CancellationToken ct)
    {
        var path = "v1/forecast"
                   + $"?latitude={Format(latitude)}&longitude={Format(longitude)}"
                   + "&hourly=wind_speed_10m,wind_direction_10m,wind_gusts_10m,precipitation,temperature_2m"
                   + $"&past_days={pastDays}&forecast_days={forecastDays}"
                   + $"&models={model}&wind_speed_unit=kmh&timezone=Europe%2FZurich";
        var response = await SourceHttp.Get(httpClient, path, store, "open-meteo", $"forecast-{model}", ct);
        return (response, response.IsSuccess ? HourlyWeather.Parse(response.Body!) : null);
    }

    private static string Format(double value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}

/// <summary>
/// One hour of weather at a point. Times are local (Europe/Zurich).
/// </summary>
public record WeatherHour(
    DateTime LocalTime,
    double? WindSpeedKmh,
    double? WindDirection,
    double? WindGustsKmh,
    double? PrecipitationMm,
    double? AirTemperature);

/// <summary>
/// The hourly weather of one model at one grid point.
/// </summary>
public record HourlyWeather(double GridLatitude, double GridLongitude, double? Elevation, IReadOnlyList<WeatherHour> Hours)
{
    public static HourlyWeather Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var hourly = root.GetProperty("hourly");
        var times = hourly.GetProperty("time").EnumerateArray()
            .Select(t => DateTime.Parse(t.GetString()!, CultureInfo.InvariantCulture))
            .ToList();

        var hours = times.Select((time, i) => new WeatherHour(
                time,
                ValueAt(hourly, "wind_speed_10m", i),
                ValueAt(hourly, "wind_direction_10m", i),
                ValueAt(hourly, "wind_gusts_10m", i),
                ValueAt(hourly, "precipitation", i),
                ValueAt(hourly, "temperature_2m", i)))
            .ToList();

        return new HourlyWeather(
            root.GetProperty("latitude").GetDouble(),
            root.GetProperty("longitude").GetDouble(),
            root.TryGetProperty("elevation", out var elevation) ? elevation.GetDouble() : null,
            hours);
    }

    private static double? ValueAt(JsonElement hourly, string variable, int index)
    {
        if (!hourly.TryGetProperty(variable, out var values) || index >= values.GetArrayLength())
        {
            return null;
        }

        var value = values[index];
        return value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
    }
}
