using Tauchplaetze.Core.Features.Forecasts.Sources.OpenMeteo;

namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Das Wetter pro Stunde für die Grafik auf der Platzseite (`forecast.md`, «Wetter pro Stunde»).</summary>
internal static class WeatherOutlook
{
    /// <summary>
    /// Jede Stunde von <paramref name="from"/> an, <paramref name="days"/> Tage lang. Jeder Wert kommt aus dem ersten Modell, das für diese
    /// Stunde einen hat (Modelle nach Vorrang, das feinste zuerst). Fehlt er in allen, bleibt er leer.
    /// </summary>
    public static IReadOnlyList<WeatherHourForecast> For(DateOnly from, int days, IReadOnlyList<ModelWeather> models)
    {
        var byTime = models.Select(m => m.Hours.ToDictionary(h => h.LocalTime)).ToList();
        var start = from.ToDateTime(TimeOnly.MinValue);

        return Enumerable.Range(0, days * 24)
            .Select(offset => start.AddHours(offset))
            .Select(time =>
            {
                var hours = byTime.Select(m => m.GetValueOrDefault(time)).OfType<WeatherHour>().ToList();
                return new WeatherHourForecast(
                    time,
                    Round(hours.Select(h => h.TemperatureC).FirstOrDefault(v => v is not null)),
                    Round(hours.Select(h => h.PrecipitationMm).FirstOrDefault(v => v is not null)),
                    hours.Select(h => h.SunshineSeconds).FirstOrDefault(v => v is not null) is { } seconds ? (int)Math.Round(seconds / 60) : null,
                    hours.Select(h => h.WeatherCode).FirstOrDefault(v => v is not null) is { } code ? WeatherSymbols.FromWmo(code) : null,
                    hours.Select(h => h.IsDay).FirstOrDefault(v => v is not null),
                    Round(hours.Select(h => h.WindSpeedKmh).FirstOrDefault(v => v is not null)),
                    Round(hours.Select(h => h.WindGustsKmh).FirstOrDefault(v => v is not null)),
                    hours.Select(h => h.WindDirection).FirstOrDefault(v => v is not null) is { } direction ? (int)Math.Round(direction) % 360 : null);
            })
            .ToList();
    }

    private static decimal? Round(double? value) => value is { } v ? Math.Round((decimal)v, 1) : null;
}
