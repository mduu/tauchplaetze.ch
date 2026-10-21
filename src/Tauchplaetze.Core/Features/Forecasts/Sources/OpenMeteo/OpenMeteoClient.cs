using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Tauchplaetze.Core.Features.Forecasts.Sources.OpenMeteo;

/// <summary>Client für die Wettermodelle über Open-Meteo (ADR-007).</summary>
internal class OpenMeteoClient(HttpClient httpClient, OpenMeteoBudget budget, TimeProvider time, ILogger<OpenMeteoClient> logger)
{
    public const string Source = "open-meteo";

    /// <summary>Open-Meteo sagt beim Minutenlimit «try again in one minute».</summary>
    public static readonly TimeSpan MinutelyPause = TimeSpan.FromSeconds(61);

    /// <summary>
    /// Stündliches Wetter eines Modells an mehreren Punkten in einem Call, <paramref name="pastDays"/> Tage zurück und <paramref name="forecastDays"/> voraus.
    /// Die Liste hat die Reihenfolge der Punkte. Zeiten in Lokalzeit.
    /// Der Call wartet auf das Kontingent der Minute; ein 429 wegen des Minutenlimits wiederholt er einmal nach <see cref="MinutelyPause"/>.
    /// </summary>
    public async Task<SourceResult<IReadOnlyList<HourlyWeather>>> GetHourly(
        string model, IReadOnlyList<(double Latitude, double Longitude)> points, int pastDays, int forecastDays, CancellationToken ct)
    {
        var url = "v1/forecast"
            + $"?latitude={string.Join(',', points.Select(p => SourceHttp.Format(p.Latitude)))}"
            + $"&longitude={string.Join(',', points.Select(p => SourceHttp.Format(p.Longitude)))}"
            + "&hourly=wind_speed_10m,wind_direction_10m,wind_gusts_10m,precipitation,temperature_2m,weather_code,sunshine_duration,is_day"
            + $"&past_days={pastDays}&forecast_days={forecastDays}"
            + $"&models={model}&wind_speed_unit=kmh&timezone=Europe%2FZurich";

        if (budget.IsBlocked)
        {
            return Skipped(url);
        }

        var result = await Fetch(url, points.Count, ct);
        if (LimitOf(result) is QuotaLimit.Minute)
        {
            logger.LogWarning("Open-Meteo minute limit reached for {Model}, retrying in {Pause}", model, MinutelyPause);
            await Task.Delay(MinutelyPause, time, ct);
            result = await Fetch(url, points.Count, ct);
        }

        if (LimitOf(result) is QuotaLimit.Hour or QuotaLimit.Day)
        {
            var until = LimitOf(result) is QuotaLimit.Hour ? NextHour(time.GetUtcNow()) : NextDay(time.GetUtcNow());
            budget.BlockUntil(until);
            logger.LogWarning("Open-Meteo quota exhausted, no further calls until {Until}", until);
        }

        return result;
    }

    private async Task<SourceResult<IReadOnlyList<HourlyWeather>>> Fetch(string url, int points, CancellationToken ct)
    {
        await budget.Take(points, ct);
        return await SourceHttp.Get(httpClient, url, json => HourlyWeather.ParseMany(json, points), ct);
    }

    private SourceResult<IReadOnlyList<HourlyWeather>> Skipped(string url) =>
        new(new SourceResponse(new Uri(httpClient.BaseAddress!, url).ToString(), null, null, "Kontingent von Open-Meteo erschöpft, Call übersprungen", TimeSpan.Zero), null);

    private static QuotaLimit? LimitOf(SourceResult<IReadOnlyList<HourlyWeather>> result) =>
        result.Response.StatusCode != (int)HttpStatusCode.TooManyRequests ? null
        : result.Response.Error?.Contains("Daily", StringComparison.OrdinalIgnoreCase) == true ? QuotaLimit.Day
        : result.Response.Error?.Contains("Hourly", StringComparison.OrdinalIgnoreCase) == true ? QuotaLimit.Hour
        : QuotaLimit.Minute;

    private static DateTimeOffset NextHour(DateTimeOffset now) => Truncated(now, TimeSpan.FromHours(1)).AddHours(1);

    private static DateTimeOffset NextDay(DateTimeOffset now) => Truncated(now, TimeSpan.FromDays(1)).AddDays(1);

    private static DateTimeOffset Truncated(DateTimeOffset now, TimeSpan unit) =>
        new(now.UtcTicks - now.UtcTicks % unit.Ticks, TimeSpan.Zero);

    private enum QuotaLimit
    {
        Minute,
        Hour,
        Day
    }
}

/// <summary>Eine Stunde Wetter am Punkt, Lokalzeit (Europe/Zurich). <see cref="WeatherCode"/> nach WMO, Sonne in Sekunden der Stunde.</summary>
internal record WeatherHour(
    DateTime LocalTime,
    double? WindSpeedKmh,
    double? WindDirection,
    double? WindGustsKmh,
    double? PrecipitationMm,
    double? TemperatureC = null,
    int? WeatherCode = null,
    double? SunshineSeconds = null,
    bool? IsDay = null);

/// <summary>Das stündliche Wetter eines Modells an einem Gitterpunkt.</summary>
internal record HourlyWeather(IReadOnlyList<WeatherHour> Hours)
{
    public static HourlyWeather Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Of(document.RootElement);
    }

    /// <summary>Bei mehreren Punkten liefert Open-Meteo eine Liste, bei einem ein Objekt. Fehlt ein Punkt, ist die Antwort ungültig.</summary>
    public static IReadOnlyList<HourlyWeather> ParseMany(string json, int expected)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var all = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().Select(Of).ToList() : [Of(root)];
        return all.Count == expected
            ? all
            : throw new FormatException($"Open-Meteo lieferte {all.Count} statt {expected} Punkte.");
    }

    private static HourlyWeather Of(JsonElement location)
    {
        var hourly = location.GetProperty("hourly");
        var times = hourly.GetProperty("time").EnumerateArray()
            .Select(t => DateTime.Parse(t.GetString()!, CultureInfo.InvariantCulture))
            .ToList();

        return new HourlyWeather(times
            .Select((time, i) => new WeatherHour(
                time,
                ValueAt(hourly, "wind_speed_10m", i),
                ValueAt(hourly, "wind_direction_10m", i),
                ValueAt(hourly, "wind_gusts_10m", i),
                ValueAt(hourly, "precipitation", i),
                ValueAt(hourly, "temperature_2m", i),
                ValueAt(hourly, "weather_code", i) is { } code ? (int)code : null,
                ValueAt(hourly, "sunshine_duration", i),
                ValueAt(hourly, "is_day", i) is { } day ? day > 0 : null))
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
