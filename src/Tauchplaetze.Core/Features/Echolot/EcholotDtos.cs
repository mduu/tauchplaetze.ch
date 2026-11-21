namespace Tauchplaetze.Core.Features.Echolot;

/// <summary>Die Seite «Echolot»: der Stand jeder Datenquelle und die letzten Neuigkeiten (`ux.md`, «Echolot»).</summary>
/// <param name="HasOlderEntries">Es gibt ältere Neuigkeiten als die gezeigten.</param>
/// <param name="Sounding">Das Beispiel für die Echolot-Anzeige. <c>null</c>: noch keine Prognose mit Sprungschicht.</param>
public record EcholotReport(IReadOnlyList<DataSourceStatus> Sources, IReadOnlyList<EcholotEntry> Entries, bool HasOlderEntries, EcholotSounding? Sounding = null);

/// <summary>Ein Platz mit Sprungschicht als Beispiel für die Echolot-Anzeige, mit dem Tag der Prognose. Temperaturen in °C, Tiefen in m.</summary>
public record EcholotSounding(
    string Slug,
    string Name,
    string WaterBodyName,
    DateOnly Date,
    decimal ThermoclineTop,
    decimal ThermoclineBottom,
    decimal? SurfaceTemperature,
    decimal? TemperatureAt30m);

/// <summary>
/// Der Stand einer Datenquelle. <see cref="UpdatedAt"/>: letzte Lieferung. <see cref="Count"/>: Plätze oder Stationen, wo sinnvoll.
/// <see cref="ObservedOn"/>: beim Satellit der Tag des neuesten Bilds.
/// </summary>
public record DataSourceStatus(DataSource Source, DataHealth Health, DateTimeOffset? UpdatedAt, int? Count = null, DateOnly? ObservedOn = null);

/// <summary>
/// Ein Eintrag der Neuigkeiten. Eine neue Funktion hat <see cref="Title"/> und <see cref="Text"/>,
/// erstmals beschriebene Plätze haben <see cref="Sites"/> (alle eines Tages).
/// </summary>
public record EcholotEntry(DateOnly Date, EcholotEntryKind Kind, string? Title, string? Text, IReadOnlyList<DescribedSite> Sites);

/// <summary>Ein Platz, der an dem Tag zum ersten Mal eine Beschreibung bekam.</summary>
public record DescribedSite(string Slug, string Name, string WaterBodyName);

/// <summary>Datenquelle, wie Besucher sie kennen.</summary>
public enum DataSource
{
    /// <summary>Der Tauch-Index aus dem Prognose-Lauf.</summary>
    Forecast,

    /// <summary>Seemodelle von Alplakes (Eawag).</summary>
    LakeModels,

    /// <summary>Wetter von MeteoSchweiz über Open-Meteo.</summary>
    Weather,

    /// <summary>Satellitenbilder über Alplakes.</summary>
    Satellite,

    /// <summary>Abfluss der Flüsse (BAFU).</summary>
    Discharge,

    /// <summary>Seepegel (BAFU).</summary>
    LakeLevel,
}

/// <summary>Zustand einer Datenquelle (`ux.md`, «Echolot»).</summary>
public enum DataHealth
{
    /// <summary>Aktuell.</summary>
    Current,

    /// <summary>Verspätet oder nur teilweise geliefert.</summary>
    Late,

    /// <summary>Gestört: lange keine Lieferung oder alle Calls gescheitert.</summary>
    Down,

    /// <summary>Noch nie geliefert.</summary>
    Unknown,
}

/// <summary>Art eines Eintrags der Neuigkeiten.</summary>
public enum EcholotEntryKind
{
    /// <summary>Neue Funktion aus `news.json`.</summary>
    Feature,

    /// <summary>Plätze mit erster Beschreibung.</summary>
    Described,
}
