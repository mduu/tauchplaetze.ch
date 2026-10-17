namespace Tauchplaetze.Spike.DataSources;

/// <summary>
/// The availability of one data category.
/// </summary>
public enum CategoryStatus
{
    /// <summary>The data came as expected.</summary>
    Ok,

    /// <summary>Some data came, but with gaps or old values.</summary>
    Partial,

    /// <summary>The source answered, but has no data for this site.</summary>
    Missing,

    /// <summary>The source did not answer.</summary>
    Failed
}

/// <summary>
/// The result of the spike for one dive site.
/// </summary>
public record SpikeReport(
    string Site,
    double Latitude,
    double Longitude,
    DateTimeOffset GeneratedAt,
    string RawDataDirectory,
    IReadOnlyList<CategoryResult> Categories,
    IReadOnlyList<DayConditions> Days,
    SatelliteSummary Satellite,
    IReadOnlyList<HydroSummary> Hydro,
    CoverageSummary Coverage);

/// <summary>
/// The result of one data category with all source calls.
/// </summary>
public record CategoryResult(
    string Category,
    CategoryStatus Status,
    string Summary,
    DateTimeOffset? LatestDataAt,
    IReadOnlyList<CallInfo> Calls);

/// <summary>
/// One call to a source.
/// </summary>
public record CallInfo(string Url, int? StatusCode, long DurationMs, string? Error)
{
    public static CallInfo From(SourceResponse response) =>
        new(response.Url, response.StatusCode, (long)response.Duration.TotalMilliseconds, response.Error);
}

/// <summary>
/// The derived values of one local day, as the forecast job would store them (specs/datastructure.md, DailyForecast).
/// </summary>
public record DayConditions(
    DateOnly Date,
    string? Model3d,
    double? SurfaceTemperature,
    double? TemperatureAt10m,
    double? TemperatureAt20m,
    double? TemperatureAt30m,
    double? ThermoclineTop,
    double? ThermoclineBottom,
    string? WeatherModel,
    int? WindBeaufortMax,
    double? WindSpeedMaxKmh,
    int? WindGustsBeaufortMax,
    double? WindGustsMaxKmh,
    double? WindDirectionAtMax,
    double? PrecipitationDayMm,
    double? PrecipitationLast72hMm,
    IReadOnlyDictionary<string, int?> WindBeaufortByModel);

/// <summary>
/// The latest satellite values near the site.
/// </summary>
public record SatelliteSummary(
    double? SecchiDepth,
    DateTimeOffset? SecchiAt,
    double? Chlorophyll,
    DateTimeOffset? ChlorophyllAt,
    int SecchiImagesInLookback,
    int UsableSecchiImagesAtPoint,
    string Note);

/// <summary>
/// The discharge or level of one station compared with its median.
/// </summary>
public record HydroSummary(
    string StationId,
    string Name,
    string Parameter,
    double? Latest,
    DateTimeOffset? LatestAt,
    double? Median,
    double? RatioToMedian);

/// <summary>
/// The lakes and products that the sources know.
/// </summary>
public record CoverageSummary(
    IReadOnlyDictionary<string, IReadOnlyList<string>> LakesBy3dModel,
    IReadOnlyList<string> LakesWith1dModel,
    IReadOnlyDictionary<string, IReadOnlyList<string>> RemoteSensingVariablesBySatellite,
    IReadOnlyList<string> ModelPeriods);
