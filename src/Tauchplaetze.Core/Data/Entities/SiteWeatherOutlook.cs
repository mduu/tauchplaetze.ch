namespace Tauchplaetze.Core.Data.Entities;

internal class SiteWeatherOutlook
{
    public Guid DiveSiteId { get; set; }

    public DateOnly FromDate { get; set; }

    /// <summary>Die Stunden als JSON (<c>WeatherHourForecast</c>), ab <see cref="FromDate"/> 00:00 Lokalzeit.</summary>
    public required string Hours { get; set; }

    public Guid ForecastRunId { get; set; }

    public DateTimeOffset ComputedAt { get; set; }
}
