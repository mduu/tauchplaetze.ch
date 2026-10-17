namespace Tauchplaetze.Spike.DataSources;

/// <summary>
/// Settings of the data source spike.
/// </summary>
public class SpikeOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Spike";

    /// <summary>
    /// The User-Agent header with a contact address. The sources can identify us.
    /// </summary>
    public string UserAgent { get; set; } = "tauchplaetze.ch-spike/0.1";

    /// <summary>
    /// The directory for the raw responses. Relative paths start at the content root.
    /// </summary>
    public string OutputDirectory { get; set; } = "spike-output";

    /// <summary>
    /// The number of forecast days to request, today included.
    /// </summary>
    public int ForecastDays { get; set; } = 5;

    /// <summary>
    /// The number of days to look back for satellite images.
    /// </summary>
    public int SatelliteLookbackDays { get; set; } = 30;

    /// <summary>
    /// The number of days to look back for the discharge median.
    /// </summary>
    public int HydroLookbackDays { get; set; } = 30;

    /// <summary>
    /// The temperature gradient in °C per metre that marks the thermocline.
    /// </summary>
    public double ThermoclineGradientThreshold { get; set; } = 0.5;

    /// <summary>
    /// The weather models in order of preference. A day takes its wind and rain from the first model with data.
    /// </summary>
    public List<WeatherModel> WeatherModels { get; set; } = [];

    /// <summary>
    /// The dive sites to check.
    /// </summary>
    public List<SpikeSite> Sites { get; set; } = [];
}

/// <summary>
/// A dive site with the source keys that the spike needs.
/// </summary>
public class SpikeSite
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string Municipality { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>
    /// A point further offshore for the satellite products. Pixels near the shore are often empty.
    /// </summary>
    public double? SatelliteLatitude { get; set; }

    /// <summary>
    /// See <see cref="SatelliteLatitude"/>.
    /// </summary>
    public double? SatelliteLongitude { get; set; }

    public string AlplakesLake { get; set; } = "";
    public List<string> Alplakes3dModels { get; set; } = [];
    public string? Alplakes1dLake { get; set; }
    public List<HydroStation> HydroStations { get; set; } = [];
}

/// <summary>
/// A weather model of Open-Meteo with its lead time.
/// </summary>
public class WeatherModel
{
    /// <summary>
    /// The Open-Meteo model key, for example <c>meteoswiss_icon_ch2</c>.
    /// </summary>
    public string Model { get; set; } = "";

    /// <summary>
    /// A short name for the report, for example <c>MeteoSwiss ICON-CH2</c>.
    /// </summary>
    public string Label { get; set; } = "";

    /// <summary>
    /// The number of forecast days to request, today included.
    /// </summary>
    public int ForecastDays { get; set; }
}

/// <summary>
/// A BAFU hydrology station.
/// </summary>
public class HydroStation
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>
    /// <c>flow</c> (m³/s) or <c>height</c> (m a.s.l.).
    /// </summary>
    public string Parameter { get; set; } = "flow";
}
