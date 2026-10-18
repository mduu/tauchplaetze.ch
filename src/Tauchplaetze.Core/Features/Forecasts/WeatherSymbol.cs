namespace Tauchplaetze.Core.Features.Forecasts;

/// <summary>Wetterlage einer Stunde als Symbol (`forecast.md`, «Wetter pro Stunde»). Die Reihenfolge ist die Schwere.</summary>
public enum WeatherSymbol
{
    /// <summary>Klar.</summary>
    Clear,

    /// <summary>Leicht bis teilweise bewölkt.</summary>
    PartlyCloudy,

    /// <summary>Bedeckt.</summary>
    Cloudy,

    /// <summary>Nebel.</summary>
    Fog,

    /// <summary>Niesel.</summary>
    Drizzle,

    /// <summary>Regen.</summary>
    Rain,

    /// <summary>Regenschauer.</summary>
    Showers,

    /// <summary>Schnee.</summary>
    Snow,

    /// <summary>Gewitter.</summary>
    Thunderstorm,
}

/// <summary>Übersetzt den Wettercode nach WMO 4677 (Open-Meteo `weather_code`) in ein Symbol.</summary>
public static class WeatherSymbols
{
    /// <summary>Das Symbol zum Code. Ein unbekannter Code ergibt <c>null</c>.</summary>
    public static WeatherSymbol? FromWmo(int code) => code switch
    {
        0 => WeatherSymbol.Clear,
        1 or 2 => WeatherSymbol.PartlyCloudy,
        3 => WeatherSymbol.Cloudy,
        45 or 48 => WeatherSymbol.Fog,
        >= 51 and <= 57 => WeatherSymbol.Drizzle,
        >= 61 and <= 67 => WeatherSymbol.Rain,
        >= 71 and <= 77 or 85 or 86 => WeatherSymbol.Snow,
        >= 80 and <= 82 => WeatherSymbol.Showers,
        >= 95 and <= 99 => WeatherSymbol.Thunderstorm,
        _ => null,
    };
}
