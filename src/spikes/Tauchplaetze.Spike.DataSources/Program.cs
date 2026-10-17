using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Tauchplaetze.Spike.DataSources;
using Tauchplaetze.Spike.DataSources.Alplakes;
using Tauchplaetze.Spike.DataSources.Hydro;
using Tauchplaetze.Spike.DataSources.Weather;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOptions<SpikeOptions>()
    .Bind(builder.Configuration.GetSection(SpikeOptions.SectionName))
    .Validate(o => o.Sites.Count > 0, "Configure at least one site in Spike:Sites.")
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<RawResponseStore>();
builder.Services.AddScoped<DataSourceCheck>();

AddSourceClient<AlplakesClient>(builder, "https://alplakes-api.eawag.ch/");
AddSourceClient<OpenMeteoClient>(builder, "https://api.open-meteo.com/");
AddSourceClient<HydroClient>(builder, "https://api.existenz.ch/");

// The ServiceDefaults add the standard resilience handler to every client. Its defaults (10 s per attempt, 30 s in total)
// are too short for the sources: Alplakes needs up to 30 s for one depthtime call, Open-Meteo answers 503 when overloaded.
builder.Services.ConfigureAll<HttpStandardResilienceOptions>(o =>
{
    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
    o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(240);
    o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
    o.Retry.MaxRetryAttempts = 4;
    o.Retry.Delay = TimeSpan.FromSeconds(2);
});

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.WriteIndented = true;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/", (IOptions<SpikeOptions> options) => Results.Ok(new
{
    description = "Spike Phase 0.5: checks Alplakes, MeteoSwiss (via Open-Meteo) and BAFU (via existenz.ch) for one dive site.",
    reports = options.Value.Sites.Select(s => $"/report/{s.Key}")
}));

app.MapGet("/report/{siteKey}", async (string siteKey, DataSourceCheck check, RawResponseStore store, IOptions<SpikeOptions> options, CancellationToken ct) =>
{
    var site = options.Value.Sites.FirstOrDefault(s => s.Key.Equals(siteKey, StringComparison.OrdinalIgnoreCase));
    if (site is null)
    {
        return Results.NotFound();
    }

    var report = await check.Run(site, ct);
    await store.SaveReport(
        JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } }),
        ct);
    return Results.Ok(report);
});

app.Run();

static void AddSourceClient<TClient>(WebApplicationBuilder builder, string baseAddress)
    where TClient : class
{
    builder.Services.AddHttpClient<TClient>((services, client) =>
    {
        client.BaseAddress = new Uri(baseAddress);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(services.GetRequiredService<IOptions<SpikeOptions>>().Value.UserAgent);
    });
}
