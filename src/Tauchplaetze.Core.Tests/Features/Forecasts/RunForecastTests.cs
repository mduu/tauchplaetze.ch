using Tauchplaetze.Core.CrossCutting.Geo;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Commands;
using Tauchplaetze.Core.Features.Forecasts.Queries;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Features.WaterBodies.Queries;
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
    public async Task Ein_Lauf_speichert_die_Stroemung_aus_dem_Seemodell_und_ohne_Werte_bleibt_sie_leer()
    {
        // Arrange: Die Antwort vom 17.10.2026 hatte noch keine Strömung (alle `u`/`v` leer).
        var siteId = await PrepareRiffWilliams();
        await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());
        var withoutCurrent = (await Query<GetSiteForecastQuery, SiteForecast>(new GetSiteForecastQuery(siteId))).Days[0];
        Sql.Sources.Overrides["simulations/depthtime/delft3d-flow/"] = WithCurrent(TestData.Read("alplakes", "depthtime-delft3d-flow-zurich.json"), east: 0.06, north: 0.08);

        // Act
        await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Assert
        var today = (await Query<GetSiteForecastQuery, SiteForecast>(new GetSiteForecastQuery(siteId))).Days[0];
        Assert.Equal((null, null, null), (withoutCurrent.CurrentSpeed, withoutCurrent.CurrentDirection, withoutCurrent.CurrentLevel));
        Assert.Equal((0.1m, 37, CurrentLevel.Noticeable), (today.CurrentSpeed, today.CurrentDirection, today.CurrentLevel));
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
    public async Task Das_Wetter_aller_Plaetze_kommt_mit_einem_Call_pro_Modell_und_jeder_Platz_hat_seine_Stunden()
    {
        // Arrange
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var admin = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        var lake = (await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery("de"))).Single(w => w.Slug == "zuerichsee").Id;
        var sites = new List<Guid> { await AddRiffWilliams(admin) };
        foreach (var (name, latitude, longitude) in new[] { ("Ermitage", 47.3360, 8.5880), ("Halbinsel Au", 47.2500, 8.6400) })
        {
            var proposed = await Command<ProposeDiveSiteCommand, EditorialResult>(new ProposeDiveSiteCommand(admin, name, lake, latitude, longitude, null, "ZH", null));
            sites.Add(proposed.DiveSiteId!.Value);
        }

        // Act
        await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Assert
        Assert.Single(Sql.Sources.Requests, url => url.Contains("models=meteoswiss_icon_ch2", StringComparison.Ordinal));
        foreach (var site in sites)
        {
            var forecast = await Query<GetSiteForecastQuery, SiteForecast>(new GetSiteForecastQuery(site));
            Assert.NotEmpty(forecast.HourList);
        }
    }

    [SqlFact]
    public async Task Ein_bestehender_See_ohne_Schluessel_bekommt_beim_Seed_die_von_Alplakes_und_ein_eigener_Schluessel_bleibt()
    {
        // Arrange
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        await Sql.Arrange(async db =>
        {
            var thun = await db.WaterBodies.SingleAsync(w => w.Slug == "thunersee");
            (thun.AlplakesLakeKey, thun.AlplakesSimstratKey, thun.SatellitePoint) = (null, "thun-eigen", null);
        });
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var admin = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        var lake = (await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery("de"))).Single(w => w.Slug == "thunersee").Id;
        await Command<ProposeDiveSiteCommand, EditorialResult>(new ProposeDiveSiteCommand(admin, "Spiez Bucht", lake, 46.6890, 7.6880, "Spiez", "BE", null));

        // Act
        await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Assert
        Assert.Contains(Sql.Sources.Requests, url => url.Contains("simulations/1d/depthtime/simstrat/thun-eigen/", StringComparison.Ordinal));
        Assert.Contains(Sql.Sources.Requests, url => url.Contains("remotesensing/timeseries/thun/sentinel2/Z490/", StringComparison.Ordinal));
        Assert.Contains(Sql.Sources.Requests, url => url.Contains("remotesensing/timeseries/thun/sentinel3/Zsd_lee/46.6900/7.7200/", StringComparison.Ordinal));
    }

    [SqlFact]
    public async Task Ohne_Satellitenpunkt_fragt_der_Lauf_Sentinel_3_nicht_am_Ufer_ab()
    {
        // Arrange
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var admin = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        var lake = (await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery("de"))).Single(w => w.Slug == "silsersee").Id;
        await Command<ProposeDiveSiteCommand, EditorialResult>(new ProposeDiveSiteCommand(admin, "Isola", lake, 46.4180, 9.7470, "Sils", "GR", null));

        // Act
        await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Assert
        Assert.DoesNotContain(Sql.Sources.Requests, url => url.Contains("/sentinel3/", StringComparison.Ordinal));
        Assert.Contains(Sql.Sources.Requests, url => url.Contains("remotesensing/timeseries/sils/sentinel2/Z490/", StringComparison.Ordinal));
    }

    [SqlFact]
    public async Task Bricht_der_Host_einen_Lauf_ab_bleiben_die_Werte_des_letzten_Laufs_und_er_zaehlt_nicht_als_letzter_Lauf()
    {
        // Arrange
        var siteId = await PrepareRiffWilliams();
        var complete = await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());
        using var shutdown = new CancellationTokenSource();
        Sql.Sources.OnRequest = url =>
        {
            if (url.Contains("alplakes", StringComparison.Ordinal) || url.Contains("simulations", StringComparison.Ordinal))
            {
                shutdown.Cancel();
            }
        };

        // Act
        var aborted = await Record.ExceptionAsync(() => Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand(), shutdown.Token));

        // Assert
        Assert.IsAssignableFrom<OperationCanceledException>(aborted);
        var last = await Query<GetLastForecastRunQuery, LastForecastRun?>(new GetLastForecastRunQuery());
        Assert.Equal(complete.RunId, last!.Id);
        var today = (await Query<GetSiteForecastQuery, SiteForecast>(new GetSiteForecastQuery(siteId))).Days[0];
        Assert.Equal(3, today.DataLevel);
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

    [SqlFact]
    public async Task Ein_Lauf_schreibt_den_Median_des_Gewaessers_fuer_heute_und_ein_zweiter_Lauf_am_selben_Tag_keine_zweite_Zeile()
    {
        // Arrange
        var siteId = await PrepareRiffWilliams();
        var lake = (await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery("de"))).Single(w => w.Slug == "zuerichsee").Id;

        // Act
        await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());
        await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Assert
        var today = (await Query<GetSiteForecastQuery, SiteForecast>(new GetSiteForecastQuery(siteId))).Days[0];
        var point = Assert.Single((await Query<GetWaterBodyIndexHistoryQuery, WaterBodyIndexHistory>(new GetWaterBodyIndexHistoryQuery(lake))).Years.Single().Points);
        Assert.Equal((new DateOnly(2026, 10, 17), today.DiveIndex, 1), (point.Date, (decimal?)point.MedianIndex, point.Sites));
    }

    [SqlFact]
    public async Task Sagt_das_BAFU_uebermorgen_Hochwasser_am_Zufluss_voraus_sinkt_der_Index_uebermorgen_und_heute_bleibt_er()
    {
        // Arrange: Linth – Weesen (2104), Zufluss des Zürichsees. Für den 19.10.2026 ein Tag mit dem Vielfachen des üblichen Abflusses.
        var siteId = await PrepareRiffWilliams();
        await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());
        var before = (await Query<GetSiteForecastQuery, SiteForecast>(new GetSiteForecastQuery(siteId))).Days;
        await Sql.Arrange(db =>
        {
            db.HydroHours.AddRange(Enumerable.Range(0, 24).Select(hour => new HydroHour
            {
                StationId = "2104",
                Kind = HydroSeriesKind.Forecast,
                Hour = new DateTimeOffset(2026, 10, 18, 22, 0, 0, TimeSpan.Zero).AddHours(hour),
                Discharge = 2000,
                ImportedAt = FixedTime.SpikeDay,
            }));
            return db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        // Act
        await Command<RunForecastCommand, RunForecastResult>(new RunForecastCommand());

        // Assert
        var after = (await Query<GetSiteForecastQuery, SiteForecast>(new GetSiteForecastQuery(siteId))).Days;
        Assert.Equal(before[0].DiveIndex, after[0].DiveIndex);
        Assert.Equal(before[1].DiveIndex, after[1].DiveIndex);
        Assert.True(after[2].DiveIndex < before[2].DiveIndex, $"{after[2].DiveIndex} < {before[2].DiveIndex}");
        Assert.Contains(ReasonCode.TurbidAfterRain, after[2].Reasons);
    }

    private async Task<Guid> PrepareRiffWilliams()
    {
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var admins = await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]));
        return await AddRiffWilliams(admins.UserIds[0]);
    }

    private static string WithCurrent(string json, double east, double north)
    {
        var root = JsonNode.Parse(json)!;
        var variables = root["variables"]!;
        var rows = variables["temperature"]!["data"]!.AsArray();
        JsonArray Filled(double value) =>
            [.. rows.Select(row => (JsonNode)new JsonArray([.. row!.AsArray().Select(_ => (JsonNode?)value)]))];
        variables["u"] = new JsonObject { ["data"] = Filled(east), ["unit"] = "m/s" };
        variables["v"] = new JsonObject { ["data"] = Filled(north), ["unit"] = "m/s" };
        return root.ToJsonString();
    }

    private static IEnumerable<DateOnly> Days(params int[] october) => october.Select(day => new DateOnly(2026, 10, day));
}
