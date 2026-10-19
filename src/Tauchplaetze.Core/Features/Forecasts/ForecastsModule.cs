using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.CrossCutting.Modules;
using Tauchplaetze.Core.Features.Forecasts.Sources.Alplakes;
using Tauchplaetze.Core.Features.Forecasts.Sources.Hydro;
using Tauchplaetze.Core.Features.Forecasts.Sources.OpenMeteo;

namespace Tauchplaetze.Core.Features.Forecasts;

internal class ForecastsModule : ICoreModule
{
    private const string UserAgent = "tauchplaetze.ch/1.0 (+https://tauchplaetze.ch)";

    public void Register(IServiceCollection services)
    {
        AddSourceClient<AlplakesClient>(services, "https://alplakes-api.eawag.ch/");
        AddSourceClient<OpenMeteoClient>(services, "https://api.open-meteo.com/");
        AddSourceClient<HydroClient>(services, "https://api.existenz.ch/");
        services.AddScoped<ForecastSources>();
        services.AddScoped<WaterBodyIndexWriter>();
        services.AddSingleton<ForecastProgress>();
    }

    /// <remarks>
    /// Die Standardwerte (10 s pro Versuch, 30 s total) sind zu kurz: Alplakes braucht bis 30 s für einen Call,
    /// Open-Meteo antwortet bei Last mit 503 (Spike 17.10.2026). Die Service Defaults legen denselben Standard-Handler um jeden Client.
    /// Ohne <c>RemoveAllResilienceHandlers</c> brach er Calls von `mitgcm` am Zugersee nach 30 s ab (Lauf vom 19.10.2026).
    /// </remarks>
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers ist experimentell, aber der einzige Weg, den Handler der Service Defaults pro Client zu ersetzen.
    private static void AddSourceClient<TClient>(IServiceCollection services, string baseAddress)
        where TClient : class =>
        services.AddHttpClient<TClient>(client =>
            {
                client.BaseAddress = new Uri(baseAddress);
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            })
            .RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(240);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
                options.Retry.MaxRetryAttempts = 4;
                options.Retry.Delay = TimeSpan.FromSeconds(2);
            });
#pragma warning restore EXTEXP0001
}
