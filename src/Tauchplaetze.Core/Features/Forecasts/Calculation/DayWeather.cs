using Tauchplaetze.Core.Features.Forecasts.Sources.OpenMeteo;

namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Das stündliche Wetter eines Modells. Die Liste der Modelle ist nach Vorrang sortiert, das feinste zuerst.</summary>
internal record ModelWeather(string Model, IReadOnlyList<WeatherHour> Hours);

/// <summary>Wind und Regen eines Tages am Platz (`forecast.md`, «Eingaben pro Platz und Tag»).</summary>
internal record DayWeather(
    string? Model,
    double? WindMaxKmh,
    double? GustMaxKmh,
    double? WindDirection,
    double? PrecipitationInDiveWindow,
    double? PrecipitationLast72h)
{
    public int? WindBeaufort => WindMaxKmh is { } kmh ? Beaufort.FromKmh(kmh) : null;

    /// <summary>
    /// Wind aus dem ersten Modell, das das Tauchfenster ganz abdeckt. Regen pro Stunde aus dem feinsten Modell mit Wert.
    /// «Regen 72 h» zählt die drei Tage vor dem Tag, ohne den Tag selbst.
    /// </summary>
    public static DayWeather For(DateOnly day, IReadOnlyList<ModelWeather> models, DiveWindow window)
    {
        var start = day.ToDateTime(new TimeOnly(window.StartHour, 0));
        var end = day.ToDateTime(new TimeOnly(window.EndHour, 0));
        var dayStart = day.ToDateTime(TimeOnly.MinValue);

        var covering = models
            .Select(m => (m.Model, Hours: HoursWithWind(m.Hours, start, end)))
            .FirstOrDefault(m => m.Hours.Count == window.Hours);

        return new DayWeather(
            covering.Model,
            covering.Hours?.Max(h => h.WindSpeedKmh),
            covering.Hours?.Max(h => h.WindGustsKmh),
            covering.Hours?.MaxBy(h => h.WindSpeedKmh)?.WindDirection,
            RainFromBestModel(models, start, end),
            RainFromBestModel(models, dayStart.AddDays(-3), dayStart));
    }

    /// <summary>Summe des Regens. Jede Stunde nimmt den Wert des ersten Modells mit Wert. <c>null</c>, wenn einer Stunde jeder Wert fehlt.</summary>
    public static double? RainFromBestModel(IReadOnlyList<ModelWeather> models, DateTime start, DateTime end)
    {
        var total = 0.0;
        for (var hour = start; hour < end; hour = hour.AddHours(1))
        {
            var rain = models
                .Select(m => m.Hours.FirstOrDefault(h => h.LocalTime == hour)?.PrecipitationMm)
                .FirstOrDefault(value => value is not null);
            if (rain is null)
            {
                return null;
            }

            total += rain.Value;
        }

        return Math.Round(total, 1);
    }

    private static List<WeatherHour> HoursWithWind(IReadOnlyList<WeatherHour> hours, DateTime start, DateTime end) =>
        hours.Where(h => h.LocalTime >= start && h.LocalTime < end && h.WindSpeedKmh is not null).ToList();
}

/// <summary>Das Tauchfenster in Lokalzeit, Standard 08:00 bis 16:00 Uhr.</summary>
internal record DiveWindow(int StartHour, int EndHour)
{
    public static readonly DiveWindow Default = new(8, 16);

    public int Hours => EndHour - StartHour;
}
