using Tauchplaetze.Core.CrossCutting.Geo;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
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
    public async Task Ein_Lauf_berechnet_fuenf_Tage_mit_Datenstufe_3_solange_das_Seemodell_reicht_danach_mit_Satellit_und_Wetter_und_das_Wetter_pro_Stunde()
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
        Assert.Equal((1, false), (forecast.Days[4].DataLevel, forecast.Days[4].IsOutlook));
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
    public async Task Die_Klarheit_kommt_von_Sentinel_2_50_m_vor_dem_Ufer_ohne_Uferfaktor_und_ohne_zweiten_Call()
    {
        // Arrange
        var siteId = await PrepareRiffWilliams();

        // Act
        var run = await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Assert
        var today = (await Query<GetSiteForecastQuery, SiteForecast>(new GetSiteForecastQuery(siteId))).Days[0];
        Assert.Equal(ForecastRunStatus.Succeeded, run.Status);
        Assert.Equal((ClaritySource.Sentinel2Shore, new DateOnly(2026, 10, 15)), (today.ClaritySource, today.SecchiObservedAt));
        Assert.Equal(today.SecchiDepth, today.SecchiDepthShore);
        Assert.Equal(5.4m, today.SecchiDepthShore);
        Assert.Contains(Sql.Sources.Requests, url => url.Contains("/sentinel2/Z490/47.2846/8.6080/", StringComparison.Ordinal));
        Assert.DoesNotContain(Sql.Sources.Requests, url => url.Contains("/sentinel2/Z490/47.2843/8.6075/", StringComparison.Ordinal));
    }

    [SqlFact]
    public async Task Hat_der_Wert_50_m_vor_dem_Ufer_nur_2_Pixel_gilt_der_Wert_bei_100_m_mit_hoechstens_zwei_Calls()
    {
        // Arrange
        var siteId = await PrepareRiffWilliams();
        Sql.Sources.Overrides["/Z490/47.2846/8.6080/"] = """
            [{"time":"2026-10-15T10:30:19+00:00","value":{"mean":4.1,"median":4.1,"min":4.0,"max":4.2,"std":0.1,"count":2}}]
            """;

        // Act
        await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Assert
        var today = (await Query<GetSiteForecastQuery, SiteForecast>(new GetSiteForecastQuery(siteId))).Days[0];
        Assert.Equal(5.5m, today.SecchiDepthShore);
        Assert.Equal(2, Sql.Sources.Requests.Count(url => url.Contains("/sentinel2/Z490/", StringComparison.Ordinal)));
        Assert.Equal(1, Sql.Sources.Requests.Count(url => url.Contains("remotesensing/metadata", StringComparison.Ordinal)));
    }

    [SqlFact]
    public async Task Liefert_Sentinel_2_bei_50_und_100_m_keine_Werte_ist_das_kein_Fehler_und_Sentinel_3_gilt()
    {
        // Arrange
        var siteId = await PrepareRiffWilliams();
        Sql.Sources.Overrides["/sentinel2/Z490/"] = "[]";

        // Act
        var run = await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Assert
        Assert.Equal(ForecastRunStatus.Succeeded, run.Status);
        var today = (await Query<GetSiteForecastQuery, SiteForecast>(new GetSiteForecastQuery(siteId))).Days[0];
        Assert.Equal(ClaritySource.Sentinel3Lake, today.ClaritySource);
        Assert.Equal(2, Sql.Sources.Requests.Count(url => url.Contains("/sentinel2/Z490/", StringComparison.Ordinal)));
    }

    [SqlFact]
    public async Task Ein_eigener_Satellitenpunkt_gibt_fuer_Sentinel_2_nur_die_Richtung()
    {
        // Arrange
        var siteId = await PrepareRiffWilliams();
        await Sql.Arrange(async db =>
        {
            var site = await db.DiveSites.SingleAsync(d => d.Id == siteId);
            site.SatellitePoint = GeoPoints.Create(47.2849, 8.6200);
        });

        // Act
        await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Assert
        Assert.Contains(Sql.Sources.Requests, url => url.Contains("/sentinel2/Z490/47.2849/8.6092/", StringComparison.Ordinal));
        Assert.DoesNotContain(Sql.Sources.Requests, url => url.Contains("/sentinel2/Z490/47.2849/8.6200/", StringComparison.Ordinal));
    }

    [SqlFact]
    public async Task Eine_unlesbare_Antwort_ist_ein_gescheiterter_Call_der_Lauf_rechnet_weiter_und_die_Antwort_liegt_im_Ordner_des_Laufs()
    {
        // Arrange
        var siteId = await PrepareRiffWilliams();
        Sql.Sources.Overrides["remotesensing/metadata"] = "<html>Wartung</html>";

        // Act
        var run = await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Assert
        Assert.Equal(ForecastRunStatus.PartiallySucceeded, run.Status);
        Assert.Contains(Sql.RawData.Files.Keys, path => path.EndsWith("/alplakes/remotesensing-metadata.json", StringComparison.Ordinal));
        var today = (await Query<GetSiteForecastQuery, SiteForecast>(new GetSiteForecastQuery(siteId))).Days[0];
        Assert.NotNull(today.DiveIndex);
    }

    [SqlFact]
    public async Task Ohne_Sentinel_2_macht_ein_Sentinel_3_Wert_aelter_als_14_Tage_die_Klarheit_unbekannt_und_der_Index_bleibt()
    {
        // Arrange
        var siteId = await PrepareRiffWilliams();
        Sql.Sources.Failing = "/Z490/";

        // Act
        await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Assert
        var days = (await Query<GetSiteForecastQuery, SiteForecast>(new GetSiteForecastQuery(siteId))).Days;
        var today = days[0];
        Assert.Equal((VisibilityIndicator.Unknown, new DateOnly(2026, 9, 27)), (today.VisibilityIndicator, today.SecchiObservedAt));
        Assert.Equal((1, true), (days[4].DataLevel, days[4].IsOutlook));
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
    public async Task Von_Hand_startet_nur_ein_Admin_den_Prognose_Lauf_und_nur_Admins_sehen_die_Laeufe()
    {
        // Arrange
        await PrepareRiffWilliams();
        var admin = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        await Command<RegisterCommand, AccountResult>(new RegisterCommand("anna@example.org", "Felswand-auf-25m", "Anna", "de", "/c?user={user}&token={token}"));
        var anna = Uri.UnescapeDataString(new Uri(Sql.Mail.LinkTo("anna@example.org")).Query.TrimStart('?').Split('&')[0]["user=".Length..]);

        // Act
        var byContributor = await Command<StartForecastRunCommand, StartForecastRunResult>(new StartForecastRunCommand(anna));
        var runsAfterContributor = await Query<GetForecastRunsQuery, IReadOnlyList<ForecastRunSummary>>(new GetForecastRunsQuery(admin));
        var byAdmin = await Command<StartForecastRunCommand, StartForecastRunResult>(new StartForecastRunCommand(admin));
        var runsForContributor = await Query<GetForecastRunsQuery, IReadOnlyList<ForecastRunSummary>>(new GetForecastRunsQuery(anna));
        var runsForAdmin = await Query<GetForecastRunsQuery, IReadOnlyList<ForecastRunSummary>>(new GetForecastRunsQuery(admin));

        // Assert
        Assert.Equal((AccountError.NotAllowed, null), (byContributor.Error, byContributor.Run));
        Assert.Empty(runsAfterContributor);
        Assert.Equal(ForecastRunStatus.Succeeded, byAdmin.Run!.Status);
        Assert.Empty(runsForContributor);
        Assert.Single(runsForAdmin);
    }

    private async Task<Guid> PrepareRiffWilliams()
    {
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var admins = await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]));
        return await AddRiffWilliams(admins.UserIds[0]);
    }

    private static IEnumerable<DateOnly> Days(params int[] october) => october.Select(day => new DateOnly(2026, 10, day));
}
