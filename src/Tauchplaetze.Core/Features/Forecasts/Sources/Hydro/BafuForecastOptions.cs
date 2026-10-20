using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tauchplaetze.Core.Features.Forecasts.Sources.Hydro;

/// <summary>Zugang zu den Abflussvorhersagen des BAFU (`data-sources.md`, «Phase 1: Vorhersagen des BAFU»). Benutzername und Passwort nur als Secret.</summary>
public class BafuForecastOptions
{
    public const string Section = "Hydro:Forecast";

    public string BaseUrl { get; set; } = "https://www.hydrodata.ch/data/";

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool HasCredentials => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);

    /// <summary>Zugangsdaten gehen nur über HTTPS hinaus.</summary>
    public bool IsValid => !HasCredentials || Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

    /// <summary>Bindet die Optionen und bricht den Start ab, wenn Zugangsdaten über eine Adresse ohne HTTPS gingen.</summary>
    public static void Register(IServiceCollection services, IConfiguration section) =>
        services.AddOptions<BafuForecastOptions>()
            .Bind(section)
            .Validate(o => o.IsValid, $"{Section}:BaseUrl muss mit Zugangsdaten HTTPS sein.")
            .ValidateOnStart();
}
