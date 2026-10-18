using Tauchplaetze.Core.Features.Contact.Commands;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Accounts.Queries;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.DiveSites.Queries;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Queries;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Queries;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Features.WaterBodies.Queries;

namespace Tauchplaetze.Web.Tests;

/// <summary>Die Web-App im Speicher. Die Datenbank zeigt auf einen toten Port. Die Queries der Seiten kommen aus Test Doubles: Die Tests prüfen die Fassade, nicht den Core.</summary>
public sealed class WebApp : WebApplicationFactory<Program>
{
    public FakeDiveSites DiveSites { get; } = new();

    public FakeSiteForecasts Forecasts { get; } = new();

    public FakeBestSites BestSites { get; } = new();

    public FakeQuery<GetDiveSitesQuery, IReadOnlyList<DiveSiteListItem>> SiteList { get; } = new(_ => []);

    public FakeQuery<FindDiveSitesQuery, FoundDiveSites> Finder { get; } = new(_ => new FoundDiveSites([], 0, 1, 1, SiteSort.Index, [], 0, 0, null));

    public FakeQuery<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>> WaterBodyList { get; } = new(_ => []);

    public FakeQuery<GetWaterBodyGroupsQuery, IReadOnlyList<WaterBodyGroup>> WaterBodyGroups { get; } = new(_ => []);

    public FakeQuery<GetWaterBodyQuery, WaterBodyDetails?> WaterBody { get; } = new(_ => null);

    public FakeQuery<SignInCommand, SignInOutcome> SignIn { get; } = new(_ => new SignInOutcome(SignInStatus.Failed));

    public FakeQuery<SendContactMessageCommand, ContactResult> Contact { get; } = new(_ => new ContactResult([]));

    public FakeQuery<GetPersonalDataQuery, PersonalData?> PersonalData { get; } = new(_ => null);

    public FakeQuery<GetDiveSitesForEditingQuery, IReadOnlyList<EditableDiveSite>> SitesForEditing { get; } = new(_ => []);

    public FakeQuery<GetPublicRevisionsQuery, PublicRevisions?> PublicRevisions { get; } = new(_ => null);

    public FakeQuery<ComparePublicRevisionsQuery, IReadOnlyList<FieldChange>?> PublicChanges { get; } = new(_ => null);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:tauchplaetze", "Server=127.0.0.1,1;Database=tauchplaetze;User Id=test;Password=test;TrustServerCertificate=true");
        builder.UseSetting("ConnectionStrings:blobs", "UseDevelopmentStorage=true");
        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetDiveSiteQuery, DiveSiteLookup>>(DiveSites));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetSiteForecastQuery, SiteForecast>>(Forecasts));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetBestSitesQuery, BestSites>>(BestSites));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetDiveSitesQuery, IReadOnlyList<DiveSiteListItem>>>(SiteList));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<FindDiveSitesQuery, FoundDiveSites>>(Finder));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>>(WaterBodyList));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetWaterBodyGroupsQuery, IReadOnlyList<WaterBodyGroup>>>(WaterBodyGroups));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetWaterBodyQuery, WaterBodyDetails?>>(WaterBody));
            services.Replace(ServiceDescriptor.Singleton<ICommandHandler<SignInCommand, SignInOutcome>>(SignIn));
            services.Replace(ServiceDescriptor.Singleton<ICommandHandler<SendContactMessageCommand, ContactResult>>(Contact));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetPersonalDataQuery, PersonalData?>>(PersonalData));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetDiveSitesForEditingQuery, IReadOnlyList<EditableDiveSite>>>(SitesForEditing));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetPublicRevisionsQuery, PublicRevisions?>>(PublicRevisions));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<ComparePublicRevisionsQuery, IReadOnlyList<FieldChange>?>>(PublicChanges));
        });
    }

    /// <summary>Schickt ein Formular einer Seite ab: holt die Seite, liest das Antiforgery-Token und postet die Felder.</summary>
    public static async Task<HttpResponseMessage> Submit(HttpClient browser, string path, string formName, Dictionary<string, string> fields, CancellationToken ct)
    {
        var page = await browser.GetStringAsync(path, ct);
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value;
        var form = new Dictionary<string, string>(fields) { ["__RequestVerificationToken"] = token, ["_handler"] = formName };
        return await browser.PostAsync(path, new FormUrlEncodedContent(form), ct);
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

/// <summary>Eine Query mit fester Antwort. <see cref="Answer"/> lässt sich pro Test setzen.</summary>
public sealed class FakeQuery<TQuery, TResult>(Func<TQuery, TResult> answer) : IQueryHandler<TQuery, TResult>, ICommandHandler<TQuery, TResult>
{
    public Func<TQuery, TResult> Answer { get; set; } = answer;

    public TQuery? LastQuery { get; private set; }

    public Task<TResult> Handle(TQuery query, CancellationToken ct = default)
    {
        LastQuery = query;
        return Task.FromResult(Answer(query));
    }
}
