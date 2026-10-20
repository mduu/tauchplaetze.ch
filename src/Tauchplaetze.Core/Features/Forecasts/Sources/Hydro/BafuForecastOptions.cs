namespace Tauchplaetze.Core.Features.Forecasts.Sources.Hydro;

/// <summary>Zugang zu den Abflussvorhersagen des BAFU (`data-sources.md`, «Phase 1: Vorhersagen des BAFU»). Benutzername und Passwort nur als Secret.</summary>
public class BafuForecastOptions
{
    public const string Section = "Hydro:Forecast";

    public string BaseUrl { get; set; } = "https://www.hydrodata.ch/data/";

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool HasCredentials => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
}
