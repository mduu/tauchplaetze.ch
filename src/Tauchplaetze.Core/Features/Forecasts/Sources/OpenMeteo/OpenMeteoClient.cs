using System.Globalization;
using System.Text.Json;

namespace Tauchplaetze.Core.Features.Forecasts.Sources.OpenMeteo;

/// <summary>Client für die Wettermodelle über Open-Meteo (ADR-007).</summary>
internal class OpenMeteoClient(HttpClient httpClient)
{
    public const string Source = "open-meteo";

    /// <summary>Stündliches Wetter eines Modells am Punkt, <paramref name="pastDays"/> Tage zurück und <paramref name="forecastDays"/> voraus. Zeiten in Lokalzeit.</summary>
    public Task<SourceResult<HourlyWeather>> GetHourly(string model, double latitude, double longitude, int pastDays, int forecastDays, CancellationToken ct) =>
        SourceHttp.Get(
            httpClient,
            "v1/forecast"
            + $"?latitude={SourceHttp.Format(latitude)}&longitude={SourceHttp.Format(longitude)}"
            + "&hourly=wind_speed_10m,wind_direction_10m,wind_gusts_10m,precipitation"
            + $"&past_days={pastDays}&forecast_days={forecastDays}"
            + $"&models={model}&wind_speed_unit=kmh&timezone=Europe%2FZurich",
            HourlyWeather.Parse,
            ct);
}

/// <summary>Eine Stunde Wetter am Punkt, Lokalzeit (Europe/Zurich).</summary>
internal record WeatherHour(DateTime LocalTime, double? WindSpeedKmh, double? WindDirection, double? WindGustsKmh, double? PrecipitationMm);

/// <summary>Das stündliche Wetter eines Modells an einem Gitterpunkt.</summary>
internal record HourlyWeather(IReadOnlyList<WeatherHour> Hours)
{
    public static HourlyWeather Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var hourly = document.RootElement.GetProperty("hourly");
        var times = hourly.GetProperty("time").EnumerateArray()
            .Select(t => DateTime.Parse(t.GetString()!, CultureInfo.InvariantCulture))
            .ToList();

        return new HourlyWeather(times
            .Select((time, i) => new WeatherHour(
                time,
                ValueAt(hourly, "wind_speed_10m", i),
                ValueAt(hourly, "wind_direction_10m", i),
                ValueAt(hourly, "wind_gusts_10m", i),
                ValueAt(hourly, "precipitation", i)))
            .ToList());
    }

    private static double? ValueAt(JsonElement hourly, string variable, int index)
    {
        if (!hourly.TryGetProperty(variable, out var values) || index >= values.GetArrayLength())
        {
            return null;
        }

        return values[index].ValueKind == JsonValueKind.Number ? values[index].GetDouble() : null;
    }
}
