namespace Tauchplaetze.Core.CrossCutting.Time;

/// <summary>Lokalzeit Europe/Zurich. Prognosetage sind Schweizer Kalendertage (`datastructure.md`, Grundregeln).</summary>
internal static class SwissTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    public static DateOnly Today(TimeProvider time) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), Zone).DateTime);

    public static DateTimeOffset ToUtc(DateOnly day, int localHour)
    {
        var local = day.ToDateTime(new TimeOnly(localHour, 0));
        return new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUniversalTime();
    }

    public static DateOnly DayOf(DateTimeOffset time) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time, Zone).DateTime);
}
