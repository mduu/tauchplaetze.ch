using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.DiveSites.Queries;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Queries;

namespace Tauchplaetze.Web.Tests;

/// <summary>Die Web-App im Speicher. Die Datenbank zeigt auf einen toten Port. Die Queries der Seiten kommen aus Test Doubles: Die Tests prüfen die Fassade, nicht den Core.</summary>
public sealed class WebApp : WebApplicationFactory<Program>
{
    public FakeDiveSites DiveSites { get; } = new();

    public FakeSiteForecasts Forecasts { get; } = new();

    public FakeBestSites BestSites { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:tauchplaetze", "Server=127.0.0.1,1;Database=tauchplaetze;User Id=test;Password=test;TrustServerCertificate=true");
        builder.UseSetting("ConnectionStrings:blobs", "UseDevelopmentStorage=true");
        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetDiveSiteQuery, DiveSiteLookup>>(DiveSites));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetSiteForecastQuery, SiteForecast>>(Forecasts));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetBestSitesQuery, BestSites>>(BestSites));
        });
    }

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
    });
}

internal static partial class VisibleText
{
    public static string Of(string html)
    {
        var body = BodyPattern().Match(html) is { Success: true } match ? match.Groups[1].Value : html;
        var withoutScripts = ScriptPattern().Replace(body, " ");
        var text = TagPattern().Replace(withoutScripts, " ");
        return WhitespacePattern().Replace(WebUtility.HtmlDecode(text), " ").Trim();
    }

    [GeneratedRegex("<body[^>]*>(.*)</body>", RegexOptions.Singleline)]
    private static partial Regex BodyPattern();

    [GeneratedRegex("<script.*?</script>", RegexOptions.Singleline)]
    private static partial Regex ScriptPattern();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}

/// <summary>Plätze nach Slug. Unbekannte Slugs ergeben «nicht gefunden».</summary>
public sealed class FakeDiveSites : IQueryHandler<GetDiveSiteQuery, DiveSiteLookup>
{
    private readonly Dictionary<string, DiveSiteLookup> lookups = [];

    public void Add(string slug, DiveSiteLookup lookup) => lookups[slug] = lookup;

    public Task<DiveSiteLookup> Handle(GetDiveSiteQuery query, CancellationToken ct = default) =>
        Task.FromResult(lookups.GetValueOrDefault(query.Slug) ?? new DiveSiteLookup.NotFound());
}

/// <summary>Prognosen nach Platz. Ohne Eintrag ist die Prognose leer.</summary>
public sealed class FakeSiteForecasts : IQueryHandler<GetSiteForecastQuery, SiteForecast>
{
    private readonly Dictionary<Guid, SiteForecast> forecasts = [];

    public void Add(Guid diveSiteId, SiteForecast forecast) => forecasts[diveSiteId] = forecast;

    public Task<SiteForecast> Handle(GetSiteForecastQuery query, CancellationToken ct = default) =>
        Task.FromResult(forecasts.GetValueOrDefault(query.DiveSiteId) ?? new SiteForecast([], null));
}

/// <summary>Eine feste Rangliste. Merkt sich die letzte Query.</summary>
public sealed class FakeBestSites : IQueryHandler<GetBestSitesQuery, BestSites>
{
    public static readonly DateOnly Today = new(2026, 10, 18);

    public IReadOnlyList<RankedSite> Ranking { get; set; } = [];

    public GetBestSitesQuery? LastQuery { get; private set; }

    public Task<BestSites> Handle(GetBestSitesQuery query, CancellationToken ct = default)
    {
        LastQuery = query;
        var days = Enumerable.Range(0, 5).Select(Today.AddDays).ToList();
        var date = query.Date is { } wanted && days.Contains(wanted) ? wanted : Today;
        return Task.FromResult(new BestSites(date, days, Ranking));
    }
}
