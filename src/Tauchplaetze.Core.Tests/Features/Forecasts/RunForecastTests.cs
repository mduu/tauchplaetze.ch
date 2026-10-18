using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.DiveSites.Queries;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Commands;
using Tauchplaetze.Core.Features.Forecasts.Queries;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

/// <remarks>Die Quellen antworten mit den echten Antworten vom 17.10.2026 (Riff Williams). Die Uhr steht auf diesem Tag.</remarks>
public class RunForecastTests(SqlServer sql) : SqlTest(sql)
{
    [SqlFact]
    public async Task Ein_Lauf_berechnet_fuenf_Tage_mit_Index_solange_das_Seemodell_reicht_danach_Ausblick_und_das_Wetter_pro_Stunde()
    {
        // Arrange
        var siteId = await PrepareRiffWilliams();

        // Act
        var run = await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Assert
        Assert.Equal(ForecastRunStatus.Succeeded, run.Status);
        var forecast = await Query<GetSiteForecastQuery, SiteForecast>(new GetSiteForecastQuery(siteId));
        Assert.Equal(Days(17, 18, 19, 20, 21), forecast.Days.Select(d => d.Date));
        Assert.All(forecast.Days.Take(4), d => Assert.Equal((3, false), (d.DataLevel, d.IsOutlook)));
        Assert.Equal((1, true), (forecast.Days[4].DataLevel, forecast.Days[4].IsOutlook));
        Assert.Equal(new DateTime(2026, 10, 17, 0, 0, 0), forecast.HourList[0].LocalTime);
        Assert.All(forecast.HourList, h => Assert.NotNull(h.WindSpeedKmh));
    }

    [SqlFact]
    public async Task Am_ersten_Tag_stimmen_Sprungschicht_und_Temperatur_in_der_Tiefe_mit_dem_Seemodell()
    {
        // Arrange
        var siteId = await PrepareRiffWilliams();

        // Act
        await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Assert
        var today = (await Query<GetSiteForecastQuery, SiteForecast>(new GetSiteForecastQuery(siteId))).Days[0];
        Assert.Equal(ProfileSource.Model3d, today.ProfileSource);
        Assert.InRange(today.SurfaceTemperature!.Value, 18m, 19m);
        Assert.InRange(today.TemperatureAt20m!.Value, 13m, 14m);
        Assert.InRange(today.ThermoclineTop!.Value, 12m, 15m);
        Assert.NotNull(today.WindBeaufort);
    }

    [SqlFact]
    public async Task Ein_Satellitenwert_aelter_als_14_Tage_macht_die_Klarheit_unbekannt_und_der_Index_bleibt()
    {
        // Arrange
        var siteId = await PrepareRiffWilliams();

        // Act
        await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Assert
        var today = (await Query<GetSiteForecastQuery, SiteForecast>(new GetSiteForecastQuery(siteId))).Days[0];
        Assert.Equal((VisibilityIndicator.Unknown, new DateOnly(2026, 9, 27)), (today.VisibilityIndicator, today.SecchiObservedAt));
        Assert.Null(today.SecchiDepth);
        Assert.NotNull(today.DiveIndex);
        Assert.Contains(ReasonCode.ClarityUnknown, today.Reasons);
    }

    [SqlFact]
    public async Task Ein_zweiter_Lauf_am_selben_Tag_ersetzt_die_Werte_statt_neue_Tage_oder_Stunden_anzulegen()
    {
        // Arrange
        var siteId = await PrepareRiffWilliams();
        await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Act
        await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Assert
        var forecast = await Query<GetSiteForecastQuery, SiteForecast>(new GetSiteForecastQuery(siteId));
        Assert.Equal(5, forecast.Days.Count);
        Assert.Equal(120, forecast.HourList.Count);
    }

    [SqlFact]
    public async Task Faellt_eine_Quelle_aus_ist_der_Lauf_ein_Teilerfolg_und_die_uebrigen_Werte_sind_gespeichert()
    {
        // Arrange
        var siteId = await PrepareRiffWilliams();
        Sql.Sources.Failing = "simulations/depthtime/";

        // Act
        var run = await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Assert
        Assert.Equal(ForecastRunStatus.PartiallySucceeded, run.Status);
        var today = (await Query<GetSiteForecastQuery, SiteForecast>(new GetSiteForecastQuery(siteId))).Days[0];
        Assert.Equal(ProfileSource.Model1d, today.ProfileSource);
        Assert.Equal(2, today.DataLevel);
    }

    [SqlFact]
    public async Task Jede_Antwort_einer_Quelle_liegt_unveraendert_im_Ordner_des_Laufs()
    {
        // Arrange
        await PrepareRiffWilliams();

        // Act
        var run = await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Assert
        Assert.Contains(Sql.RawData.Files.Keys, path => path.StartsWith($"2026-10-17/{run.RunId}/alplakes/3d-delft3d-flow-", StringComparison.Ordinal));
        Assert.Contains(Sql.RawData.Files.Keys, path => path.StartsWith($"2026-10-17/{run.RunId}/open-meteo/meteoswiss_icon_ch2-", StringComparison.Ordinal));
    }

    [SqlFact]
    public async Task Haelt_ein_anderer_Lauf_den_Lock_rechnet_der_zweite_nicht()
    {
        // Arrange
        await PrepareRiffWilliams();
        await using var other = await Sql.RunLock.TryAcquire("forecast", TestContext.Current.CancellationToken);

        // Act
        var run = await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Assert
        Assert.True(run.Skipped);
    }

    [SqlFact]
    public async Task Von_Hand_startet_nur_ein_Admin_den_Prognose_Lauf()
    {
        // Arrange
        await PrepareRiffWilliams();
        var admin = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        await Command<RegisterCommand, AccountResult>(new RegisterCommand("anna@example.org", "Felswand-auf-25m", "Anna", "de", "/c?user={user}&token={token}"));
        var anna = Uri.UnescapeDataString(new Uri(Sql.Mail.LinkTo("anna@example.org")).Query.TrimStart('?').Split('&')[0]["user=".Length..]);

        // Act
        var byContributor = await Command<StartForecastRunCommand, StartForecastRunResult>(new StartForecastRunCommand(anna));
        var runsAfterContributor = await Query<GetForecastRunsQuery, IReadOnlyList<ForecastRunSummary>>(new GetForecastRunsQuery());
        var byAdmin = await Command<StartForecastRunCommand, StartForecastRunResult>(new StartForecastRunCommand(admin));

        // Assert
        Assert.Equal((AccountError.NotAllowed, null), (byContributor.Error, byContributor.Run));
        Assert.Empty(runsAfterContributor);
        Assert.Equal(ForecastRunStatus.Succeeded, byAdmin.Run!.Status);
    }

    private async Task<Guid> PrepareRiffWilliams()
    {
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var admins = await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]));
        await Command<ImportDiveSitesCommand, ImportDiveSitesResult>(new ImportDiveSitesCommand(admins.UserIds[0]));
        var site = await Query<GetDiveSiteQuery, DiveSiteLookup>(new GetDiveSiteQuery("riff-williams-herrliberg", "de"));
        return ((DiveSiteLookup.Found)site).Site.Id;
    }

    private static IEnumerable<DateOnly> Days(params int[] october) => october.Select(day => new DateOnly(2026, 10, day));
}
