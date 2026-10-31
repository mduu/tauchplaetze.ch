using Microsoft.Extensions.Configuration;
using Tauchplaetze.Core.Features.Contact.Commands;
using Tauchplaetze.Core.Features.Content;
using Tauchplaetze.Core.Features.Content.Queries;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Accounts.Queries;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.DiveSites.Queries;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.Editorial.Queries;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Mail;
using Tauchplaetze.Core.Features.Mail.Queries;
using Tauchplaetze.Core.Features.Media;
using Tauchplaetze.Core.Features.Media.Queries;
using Tauchplaetze.Core.Features.Forecasts.Queries;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Features.WaterBodies.Queries;
using Tauchplaetze.Core.Features.Operations.Queries;
using Tauchplaetze.Core.Features.Translations;
using Tauchplaetze.Core.Features.Translations.Queries;
using Tauchplaetze.Core.Features.Reports;
using Tauchplaetze.Core.Features.Reports.Commands;
using Tauchplaetze.Core.Features.Reports.Queries;

namespace Tauchplaetze.Web.Tests;

/// <summary>Die Web-App im Speicher. Die Datenbank zeigt auf einen toten Port. Die Queries der Seiten kommen aus Test Doubles: Die Tests prüfen die Fassade, nicht den Core.</summary>
public sealed class WebApp : WebApplicationFactory<Program>
{
    public FakeDiveSites DiveSites { get; } = new();

    public FakeSiteForecasts Forecasts { get; } = new();

    public FakeQuery<GetSiteDischargeQuery, SiteDischarge?> Discharge { get; } = new(_ => null);

    public FakeQuery<GetSiteIndexHistoryQuery, SiteIndexHistory> SiteHistory { get; } = new(_ => new SiteIndexHistory([], []));

    public FakeQuery<GetWaterBodyIndexHistoryQuery, WaterBodyIndexHistory> WaterBodyHistory { get; } = new(_ => new WaterBodyIndexHistory([], []));

    public FakeBestSites BestSites { get; } = new();

    public FakeQuery<GetReadinessQuery, Readiness> Readiness { get; } = new(_ => new Readiness(true, null, null));

    public FakeQuery<GetForecastRunStateQuery, ForecastRunState> RunState { get; } = new(_ => new ForecastRunState(false, false));

    public FakeQuery<GetSiteReportsQuery, IReadOnlyList<SiteReport>> SiteReports { get; } = new(_ => []);

    public FakeQuery<SubmitReportCommand, ReportResult> SubmitReport { get; } = new(_ => new ReportResult([], Guid.NewGuid()));

    public FakeQuery<GetSitemapEntriesQuery, SitemapEntries> SitemapEntries { get; } = new(_ => new SitemapEntries([], []));

    public FakeQuery<GetDiveSitesQuery, IReadOnlyList<DiveSiteListItem>> SiteList { get; } = new(_ => []);

    public FakeQuery<FindDiveSitesQuery, FoundDiveSites> Finder { get; } = new(_ => new FoundDiveSites([], 0, 1, 1, SiteSort.Index, [], 0, 0, null));

    public FakeQuery<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>> WaterBodyList { get; } = new(_ => []);

    public FakeQuery<GetWaterBodyGroupsQuery, IReadOnlyList<WaterBodyGroup>> WaterBodyGroups { get; } = new(_ => []);

    public FakeQuery<GetWaterBodyQuery, WaterBodyDetails?> WaterBody { get; } = new(_ => null);

    public FakeQuery<SignInCommand, SignInOutcome> SignIn { get; } = new(_ => new SignInOutcome(SignInStatus.Failed));

    public FakeQuery<SendContactMessageCommand, ContactResult> Contact { get; } = new(_ => new ContactResult([]));

    public FakeQuery<RegisterCommand, AccountResult> Register { get; } = new(_ => new AccountResult([]));

    public FakeQuery<GetPersonalDataQuery, PersonalData?> PersonalData { get; } = new(_ => null);

    public FakeQuery<GetDiveSitesForEditingQuery, IReadOnlyList<EditableDiveSite>> SitesForEditing { get; } = new(_ => []);

    public FakeQuery<GetPublicRevisionsQuery, PublicRevisions?> PublicRevisions { get; } = new(_ => null);

    public FakeQuery<GetMyProposalsQuery, IReadOnlyList<MyProposal>> MyProposals { get; } = new(_ => []);

    public FakeQuery<ComparePublicRevisionsQuery, IReadOnlyList<FieldChange>?> PublicChanges { get; } = new(_ => null);

    public FakeQuery<WithdrawRevisionCommand, EditorialResult> Withdraw { get; } = new(_ => new EditorialResult([]));

    public FakeQuery<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>> PendingRevisions { get; } = new(_ => []);

    public FakeQuery<GetDiveSiteForEditingQuery, DiveSiteForEditing?> ForEditing { get; } = new(_ => null);

    public FakeQuery<GetTranslationAllowanceQuery, TranslationAllowance> TranslationAllowance { get; } = new(_ => new TranslationAllowance(TranslationError.Unavailable, 0));

    /// <summary>Wie der Core mit dem Schlüssel «richtig» in der Konfiguration.</summary>
    public FakeQuery<ExportContentQuery, ExportContentResult> ContentExport { get; } = new(query =>
    {
        if (query.AdminId is null && query.ExportKey != "richtig")
        {
            return new ExportContentResult(ContentError.InvalidKey);
        }

        query.OpenOutput(new DateTimeOffset(2026, 10, 19, 14, 30, 0, TimeSpan.Zero));
        return new ExportContentResult(null, 1, 1, 0);
    });

    public FakeQuery<GetSiteMediaQuery, IReadOnlyList<SiteMediaItem>> SiteMedia { get; } = new(_ => []);

    /// <summary>Wie der Core beim Prüfen des Cookies: Ohne eigene Antwort gilt das Konto der letzten Anmeldung.</summary>
    public FakeQuery<GetSignedInAccountQuery, SignedInAccount?> SignedIn { get; } = new(_ => null);

    public FakeQuery<GetMailQueueStatusQuery, MailQueueStatus?> MailQueue { get; } = new(_ => null);

    public FakeQuery<GetForecastRunOverviewQuery, ForecastRunOverview?> RunOverview { get; } = new(_ => null);

    public FakeQuery<GetAccountDetailsQuery, AccountDetails?> AccountDetails { get; } = new(_ => null);

    public FakeQuery<GetAccountsQuery, IReadOnlyList<AccountSummary>> Accounts { get; } = new(_ => []);

    public WebApp() => MirrorSignIn();

    /// <summary>Das Cookie bleibt gültig, solange das Konto der letzten Anmeldung dieselbe Id hat.</summary>
    public void MirrorSignIn() =>
        SignedIn.Answer = query => SignIn.LastQuery is { } last && SignIn.Answer(last).Account is { } account && account.UserId == query.UserId ? account : null;

    /// <summary>Empfänger des Kontaktformulars im Test. Er darf nie im HTML stehen.</summary>
    public const string OperatorEmail = "betreiber@example.org";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:tauchplaetze", "Server=127.0.0.1,1;Database=tauchplaetze;User Id=test;Password=test;TrustServerCertificate=true");
        builder.UseSetting("ConnectionStrings:blobs", "UseDevelopmentStorage=true");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Operator:Email"] = OperatorEmail,
            ["Contact:MessagesPerHour"] = "1000",
            ["Accounts:RateLimit:SignInPerHour"] = "1000",
            ["Accounts:RateLimit:FormsPerHour"] = "1000",
            ["OutputCache:Minutes"] = "0",
            ["Pages:RateLimit:PerMinute"] = "0",
        }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetDiveSiteQuery, DiveSiteLookup>>(DiveSites));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetReadinessQuery, Readiness>>(Readiness));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetForecastRunStateQuery, ForecastRunState>>(RunState));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetSiteReportsQuery, IReadOnlyList<SiteReport>>>(SiteReports));
            services.Replace(ServiceDescriptor.Singleton<ICommandHandler<SubmitReportCommand, ReportResult>>(SubmitReport));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetSitemapEntriesQuery, SitemapEntries>>(SitemapEntries));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetSiteForecastQuery, SiteForecast>>(Forecasts));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetSiteDischargeQuery, SiteDischarge?>>(Discharge));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetSiteIndexHistoryQuery, SiteIndexHistory>>(SiteHistory));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetWaterBodyIndexHistoryQuery, WaterBodyIndexHistory>>(WaterBodyHistory));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetBestSitesQuery, BestSites>>(BestSites));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetDiveSitesQuery, IReadOnlyList<DiveSiteListItem>>>(SiteList));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<FindDiveSitesQuery, FoundDiveSites>>(Finder));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>>(WaterBodyList));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetWaterBodyGroupsQuery, IReadOnlyList<WaterBodyGroup>>>(WaterBodyGroups));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetWaterBodyQuery, WaterBodyDetails?>>(WaterBody));
            services.Replace(ServiceDescriptor.Singleton<ICommandHandler<SignInCommand, SignInOutcome>>(SignIn));
            services.Replace(ServiceDescriptor.Singleton<ICommandHandler<SendContactMessageCommand, ContactResult>>(Contact));
            services.Replace(ServiceDescriptor.Singleton<ICommandHandler<RegisterCommand, AccountResult>>(Register));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetPersonalDataQuery, PersonalData?>>(PersonalData));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetMyProposalsQuery, IReadOnlyList<MyProposal>>>(MyProposals));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetDiveSitesForEditingQuery, IReadOnlyList<EditableDiveSite>>>(SitesForEditing));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetPublicRevisionsQuery, PublicRevisions?>>(PublicRevisions));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<ComparePublicRevisionsQuery, IReadOnlyList<FieldChange>?>>(PublicChanges));
            services.Replace(ServiceDescriptor.Singleton<ICommandHandler<WithdrawRevisionCommand, EditorialResult>>(Withdraw));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>>(PendingRevisions));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetDiveSiteForEditingQuery, DiveSiteForEditing?>>(ForEditing));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetTranslationAllowanceQuery, TranslationAllowance>>(TranslationAllowance));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetSiteMediaQuery, IReadOnlyList<SiteMediaItem>>>(SiteMedia));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetSignedInAccountQuery, SignedInAccount?>>(SignedIn));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetMailQueueStatusQuery, MailQueueStatus?>>(MailQueue));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetForecastRunOverviewQuery, ForecastRunOverview?>>(RunOverview));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetAccountDetailsQuery, AccountDetails?>>(AccountDetails));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<GetAccountsQuery, IReadOnlyList<AccountSummary>>>(Accounts));
            services.Replace(ServiceDescriptor.Singleton<IQueryHandler<ExportContentQuery, ExportContentResult>>(ContentExport));
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

    public ForecastCoverage? Coverage { get; set; }

    public IReadOnlyList<UnrankedSite> WithoutForecast { get; set; } = [];

    public int WithoutForecastCount { get; set; }

    public IReadOnlyList<RankedSite> MountainLakeSites { get; set; } = [];

    public GetBestSitesQuery? LastQuery { get; private set; }

    public Task<BestSites> Handle(GetBestSitesQuery query, CancellationToken ct = default)
    {
        LastQuery = query;
        var days = Enumerable.Range(0, 5).Select(Today.AddDays).ToList();
        var date = query.Date is { } wanted && days.Contains(wanted) ? wanted : Today;
        return Task.FromResult(new BestSites(date, days, Ranking, Coverage, WithoutForecast, WithoutForecastCount, MountainLakeSites));
    }
}

/// <summary>Eine Query mit fester Antwort. <see cref="Answer"/> lässt sich pro Test setzen.</summary>
public sealed class FakeQuery<TQuery, TResult>(Func<TQuery, TResult> answer) : IQueryHandler<TQuery, TResult>, ICommandHandler<TQuery, TResult>
{
    public Func<TQuery, TResult> Answer { get; set; } = answer;

    public TQuery? LastQuery { get; private set; }

    public int Calls { get; private set; }

    /// <summary>Antwortet asynchron wie die Datenbank: Blazor rendert dann schon einmal vor der Antwort.</summary>
    public bool Yields { get; set; }

    public async Task<TResult> Handle(TQuery query, CancellationToken ct = default)
    {
        LastQuery = query;
        Calls++;
        if (Yields)
        {
            await Task.Delay(10, ct);
        }

        return Answer(query);
    }
}
