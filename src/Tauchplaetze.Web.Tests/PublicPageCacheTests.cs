using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.Forecasts.Commands;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Features.WaterBodies.Queries;
using Tauchplaetze.Web.CrossCutting.Caching;

namespace Tauchplaetze.Web.Tests;

public class PublicPageCacheTests(WebApp app) : IClassFixture<WebApp>
{
    private const string WaterPage = "/de/gewaesser/zuerichsee";

    private const string RiverPage = "/de/tauchplaetze/strandbad-rheinfelden";

    private readonly FakeQuery<ArchiveDiveSiteCommand, EditorialResult> archive = new(_ => new EditorialResult([]));

    private readonly FakeQuery<ImportDischargeCommand, ImportDischargeResult> importDischarge = new(_ => new ImportDischargeResult(1, 0));

    [Fact]
    public async Task Eine_Seite_kommt_aus_dem_Cache_bis_ein_Command_Inhalte_aendert()
    {
        // Arrange
        using var cached = WithCache();
        using var browser = Browser(cached);
        app.WaterBody.Answer = _ => Water("Zürichsee");
        await browser.GetStringAsync(WaterPage, TestContext.Current.CancellationToken);
        app.WaterBody.Answer = _ => Water("Zürichsee (neu)");

        // Act
        var beforeCommand = await browser.GetStringAsync(WaterPage, TestContext.Current.CancellationToken);
        await Archive(cached);
        var afterCommand = await browser.GetStringAsync(WaterPage, TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain("Zürichsee (neu)", VisibleText.Of(beforeCommand));
        Assert.Contains("Zürichsee (neu)", VisibleText.Of(afterCommand));
    }

    [Fact]
    public async Task Der_Abfluss_Job_nimmt_nur_die_Seiten_mit_Abfluss_aus_dem_Cache()
    {
        // Arrange
        using var cached = WithCache();
        using var browser = Browser(cached);
        var riverId = Guid.NewGuid();
        app.DiveSites.Add("strandbad-rheinfelden", new DiveSiteLookup.Found(new DiveSiteDetails(
            riverId, "strandbad-rheinfelden", "Strandbad", "rhein", "Rhein", WaterBodyType.River, "Rheinfelden", "AG", 47.55164, 7.7767, 0, DateTimeOffset.UtcNow, [], [])));
        app.Discharge.Answer = query => query.DiveSiteId == riverId ? DiveSitePageTests.RiverDischarge() : null;
        app.WaterBody.Answer = _ => Water("Zürichsee");
        await browser.GetStringAsync(RiverPage, TestContext.Current.CancellationToken);
        await browser.GetStringAsync(WaterPage, TestContext.Current.CancellationToken);
        app.Discharge.Answer = query => query.DiveSiteId == riverId ? DiveSitePageTests.RiverDischarge() with { StationName = "Rhein – Basel" } : null;
        app.WaterBody.Answer = _ => Water("Zürichsee (neu)");

        // Act
        await cached.Services.GetRequiredService<ICommandHandler<ImportDischargeCommand, ImportDischargeResult>>()
            .Handle(new ImportDischargeCommand(), TestContext.Current.CancellationToken);
        var river = await browser.GetStringAsync(RiverPage, TestContext.Current.CancellationToken);
        var water = await browser.GetStringAsync(WaterPage, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Rhein – Basel", VisibleText.Of(river));
        Assert.DoesNotContain("Zürichsee (neu)", VisibleText.Of(water));
    }

    [Theory]
    [InlineData("2026-10-23T08:00:00Z", 60)]
    [InlineData("2026-10-23T21:30:00Z", 30)]
    [InlineData("2026-10-25T22:45:00Z", 15)]
    public void Eine_Seite_gilt_hoechstens_bis_Mitternacht_Schweizer_Zeit(string now, int minutes)
    {
        // Act
        var lifetime = PublicPageCache.Lifetime(DateTimeOffset.Parse(now, System.Globalization.CultureInfo.InvariantCulture), TimeSpan.FromMinutes(60));

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(minutes), lifetime);
    }

    [Fact]
    public async Task Nach_dem_Kontingent_bekommt_ein_Client_429_fuer_Seiten_andere_Clients_und_Health_Checks_nicht()
    {
        // Arrange
        using var limited = app.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Pages:RateLimit:PerMinute"] = "1",
            ["Pages:RateLimit:Burst"] = "2",
            ["Contact:TrustCloudflareHeader"] = "true",
        })));
        using var scraper = Browser(limited, "203.0.113.7");
        using var visitor = Browser(limited, "203.0.113.8");

        // Act
        var first = await scraper.GetAsync("/de/gewaesser", TestContext.Current.CancellationToken);
        var second = await scraper.GetAsync("/de/gewaesser", TestContext.Current.CancellationToken);
        var third = await scraper.GetAsync("/de/gewaesser", TestContext.Current.CancellationToken);
        var health = await scraper.GetAsync("/alive", TestContext.Current.CancellationToken);
        var other = await visitor.GetAsync("/de/gewaesser", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.NotNull(third.Headers.RetryAfter);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    private WebApplicationFactory<Program> WithCache() => app.WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["OutputCache:Minutes"] = "60" }));
        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<ICommandHandler<ArchiveDiveSiteCommand, EditorialResult>>(archive));
            services.Replace(ServiceDescriptor.Singleton<ICommandHandler<ImportDischargeCommand, ImportDischargeResult>>(importDischarge));
            services.EvictPublicPagesAfterCommands();
        });
    });

    private static Task Archive(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<ICommandHandler<ArchiveDiveSiteCommand, EditorialResult>>()
            .Handle(new ArchiveDiveSiteCommand("editor", Guid.NewGuid(), true), TestContext.Current.CancellationToken);

    private static HttpClient Browser(WebApplicationFactory<Program> factory, string? client = null)
    {
        var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        if (client is not null)
        {
            browser.DefaultRequestHeaders.Add("CF-Connecting-IP", client);
        }

        return browser;
    }

    private static WaterBodyDetails Water(string name) => new(7, "zuerichsee", name, WaterBodyType.Lake, 47.28, 8.6);
}
