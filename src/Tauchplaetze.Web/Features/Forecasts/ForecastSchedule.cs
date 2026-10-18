namespace Tauchplaetze.Web.Features.Forecasts;

/// <summary>Zeitplan des Prognose-Jobs in Lokalzeit (`forecast.md`, «Der Job»).</summary>
internal class ForecastSchedule
{
    public const string Section = "Forecast";

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    /// <summary>Uhrzeiten der Läufe in Lokalzeit.</summary>
    public List<TimeOnly> Times { get; set; } = [new(4, 30), new(13, 0)];

    /// <summary>Ein Lauf direkt beim Start (lokal mit Aspire).</summary>
    public bool RunOnStartup { get; set; }

    /// <summary>Der nächste Lauf nach <paramref name="now"/>, über Mitternacht und Zeitumstellung hinweg.</summary>
    public DateTimeOffset NextRun(DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, Zone);
        var today = DateOnly.FromDateTime(local.DateTime);

        return Enumerable.Range(0, 2)
            .SelectMany(offset => Times.Select(time => today.AddDays(offset).ToDateTime(time)))
            .Select(candidate => new DateTimeOffset(candidate, Zone.GetUtcOffset(candidate)))
            .Where(candidate => candidate > now)
            .Min();
    }
}
