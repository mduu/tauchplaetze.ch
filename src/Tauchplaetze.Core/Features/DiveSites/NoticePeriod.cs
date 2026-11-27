namespace Tauchplaetze.Core.Features.DiveSites;

/// <summary>Wann ein Hinweis erscheint (`dive-site.md`, «Anzeige nach Zeitraum»).</summary>
public enum NoticeTiming
{
    /// <summary>Gilt heute: Banner.</summary>
    Today,

    /// <summary>Beginnt in den nächsten <see cref="NoticePeriod.SoonDays"/> Tagen: schmale Zeile unter dem Hero.</summary>
    Soon,

    /// <summary>Beginnt später: kleine Zeile in «Lage und Einstiege».</summary>
    Later,
}

/// <summary>
/// Zeitraum eines Hinweises oder einer Einschränkung des SUSV: «gültig ab» und «gültig bis» (inklusive, Schweizer Zeit), optional «jedes Jahr».
/// Bei «jedes Jahr» zählen Tag und Monat, auch über den Jahreswechsel (15.11. bis 31.03.).
/// </summary>
public readonly record struct NoticePeriod(DateOnly? From, DateOnly? Until, bool Yearly = false)
{
    /// <summary>So viele Tage vor dem Beginn erscheint ein Hinweis unter dem Hero (Entscheid Marc, 27.11.2026).</summary>
    public const int SoonDays = 14;

    private bool IsYearly => Yearly && From is not null && Until is not null;

    /// <summary>Der laufende oder nächste Zeitraum ab <paramref name="day"/>. Null, wenn er vorbei ist. Ein fehlendes Datum lässt die Seite offen.</summary>
    public (DateOnly? Start, DateOnly? End)? Next(DateOnly day)
    {
        if (!IsYearly)
        {
            return Until < day ? null : (From, Until);
        }

        var from = From!.Value;
        var until = Until!.Value;
        var wraps = (until.Month, until.Day).CompareTo((from.Month, from.Day)) < 0;
        for (var year = day.Year - 1; year <= day.Year + 1; year++)
        {
            var start = InYear(from, year);
            var end = InYear(until, wraps ? year + 1 : year);
            if (end >= day)
            {
                return (start, end);
            }
        }

        return null;
    }

    public bool IsActive(DateOnly day) => Next(day) is { } next && (next.Start is null || next.Start <= day);

    /// <summary>Stufe der Anzeige. Null: vorbei, unsichtbar.</summary>
    public NoticeTiming? Timing(DateOnly day) => Next(day) switch
    {
        null => null,
        { Start: null } => NoticeTiming.Today,
        { Start: { } start } when start <= day => NoticeTiming.Today,
        { Start: { } start } when start.DayNumber - day.DayNumber <= SoonDays => NoticeTiming.Soon,
        _ => NoticeTiming.Later,
    };

    private static DateOnly InYear(DateOnly date, int year) => new(year, date.Month, Math.Min(date.Day, DateTime.DaysInMonth(year, date.Month)));
}
