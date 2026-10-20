using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.Editorial.Queries;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Commands;
using Tauchplaetze.Core.Features.Forecasts.Queries;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Features.WaterBodies.Queries;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

/// <remarks>Der Platz liegt beim Strandbad Rheinfelden, 2 km von der Station 2091. Die Uhr steht auf dem 19.10.2026, 15:45 UTC. Die echte Antwort beginnt am 18.10.2026 um 00:00 UTC.</remarks>
public class DischargeTests(SqlServer sql) : SqlTest(sql)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 15, 45, 0, TimeSpan.Zero);

    [SqlFact]
    public async Task Der_Import_liest_die_echte_Antwort_von_existenz_ch_und_der_Platz_zeigt_den_neuesten_Messwert_der_naechsten_Station()
    {
        // Arrange
        var (_, site) = await PrepareRheinfelden();
        Sql.Sources.Overrides["locations=2091&"] = TestData.Read("hydro", "daterange-2091-flow-height.json");

        // Act
        var result = await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());

        // Assert
        var discharge = await Discharge(site);
        Assert.Equal((1, 0, false), (result.Stations, result.StationsWithoutValues, result.Failed));
        Assert.Equal(("2091", 325.82m), (discharge!.StationId, discharge.Discharge));
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 15, 40, 0, TimeSpan.Zero), discharge.MeasuredAt);
        Assert.False(discharge.IsStale);
        Assert.Equal(40, discharge.Hours.Count);
        Assert.Equal([new DateOnly(2026, 10, 18), new DateOnly(2026, 10, 19)], discharge.Days.Select(d => d.Date));
        Assert.Contains(Sql.RawData.Files.Keys, path => path.StartsWith("discharge/2026-10-19/1545/hydro/", StringComparison.Ordinal));
    }

    [SqlFact]
    public async Task Die_Wassertemperatur_kommt_mit_Messzeit_von_der_Station_und_bleibt_leer_wo_die_Station_keine_misst()
    {
        // Arrange: echte Antwort vom 19.10.2026, 16:10 UTC. 2605 Verzasca misst keine Temperatur.
        var (moderator, rheinfelden) = await PrepareRheinfelden();
        Sql.Time.Now = new DateTimeOffset(2026, 10, 19, 16, 15, 0, TimeSpan.Zero);
        var verzasca = (await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery("de"))).Single(w => w.Slug == "verzasca").Id;
        var posse = (await Command<ProposeDiveSiteCommand, EditorialResult>(
            new ProposeDiveSiteCommand(moderator, "Posse 1", verzasca, 46.251083, 8.843602, "Verzasca", "TI", null))).DiveSiteId!.Value;
        Sql.Sources.Overrides["locations=2091,2605&"] = TestData.Read("hydro", "daterange-2091-2605-flow-temperature.json");

        // Act
        await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());

        // Assert
        var rhein = await Discharge(rheinfelden);
        var withoutTemperature = await Discharge(posse);
        Assert.Equal(new WaterTemperature(16.6m, new DateTimeOffset(2026, 10, 19, 16, 10, 0, TimeSpan.Zero), false), rhein!.Temperature);
        Assert.Equal(6.17m, withoutTemperature!.Discharge);
        Assert.Null(withoutTemperature.Temperature);
    }

    [SqlFact]
    public async Task Steigt_der_Abfluss_in_3_Stunden_auf_das_Doppelte_des_Medians_ist_er_stark_und_steigt()
    {
        // Arrange: 30 Tage 100 m³/s, in den letzten 3 Stunden 250 m³/s.
        var (_, site) = await PrepareRheinfelden();
        Sql.Sources.Overrides["locations=2091&"] = Flow(Now.AddDays(-30), Now, t => t > Now.AddHours(-3) ? 250 : 100);

        // Act
        await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());

        // Assert
        var discharge = await Discharge(site);
        Assert.Equal((100m, DischargeLevel.Strong, false), (discharge!.Median30Days, discharge.Level, discharge.UsesSiteThresholds));
        Assert.Equal((DischargeTrend.Rising, 150m), (discharge.Trend, discharge.ChangeThreeHours));
        Assert.Equal(new DischargeThresholds(50, 100, 200), discharge.Thresholds);
    }

    [SqlFact]
    public async Task Die_Schwellen_der_Moderation_ordnen_den_Abfluss_ein_und_muessen_steigen()
    {
        // Arrange
        var (moderator, site) = await PrepareRheinfelden();
        Sql.Sources.Overrides["locations=2091&"] = Flow(Now.AddDays(-30), Now, _ => 250);
        await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());
        var edit = (await Query<GetDiveSiteForEditingQuery, DiveSiteForEditing?>(new GetDiveSiteForEditingQuery(site)))!;

        // Act
        var falling = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, edit.BaseNumber,
            edit.Content with { Discharge = new DischargeThresholds(300, 200, 800) }, null));
        var rising = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, edit.BaseNumber,
            edit.Content with { Discharge = new DischargeThresholds(200, 400, 800) }, null));

        // Assert
        var discharge = await Discharge(site);
        Assert.Equal([EditorialError.InvalidDischargeThresholds], falling.Errors);
        Assert.True(rising.Succeeded);
        Assert.Equal((DischargeLevel.Low, true), (discharge!.Level, discharge.UsesSiteThresholds));
    }

    [SqlFact]
    public async Task Ein_zweiter_Import_schreibt_dieselben_Stunden_und_Stunden_aelter_als_30_Tage_fallen_weg()
    {
        // Arrange
        await PrepareRheinfelden();
        Sql.Sources.Overrides["locations=2091&"] = Flow(Now.AddDays(-30), Now, _ => 100);
        await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());
        var first = await HourCount();

        // Act
        await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());
        var second = await HourCount();
        Sql.Time.Now = Now.AddDays(10);
        Sql.Sources.Overrides["locations=2091&"] = Flow(Now.AddDays(9), Now.AddDays(10), _ => 100);
        await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());

        // Assert
        Assert.Equal(first, second);
        Assert.Equal(1, Sql.Sources.Requests.Count(url => url.Contains("startdate=2026-09-19", StringComparison.Ordinal)));
        await Sql.Arrange(async db => Assert.False(await db.HydroHours.AnyAsync(h => h.Hour < Now.AddDays(-20), TestContext.Current.CancellationToken)));
    }

    [SqlFact]
    public async Task Ein_Platz_am_See_hat_keine_Kachel_Abfluss_aber_der_Import_holt_den_Zufluss_des_Sees()
    {
        // Arrange
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var admins = await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]));
        var site = await AddRiffWilliams(admins.UserIds[0]);

        // Act
        var result = await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());

        // Assert
        Assert.Equal(1, result.Stations);
        Assert.Null(await Discharge(site));
        Assert.Contains(Sql.Sources.Requests, url => url.Contains("locations=2104&", StringComparison.Ordinal));
    }

    [SqlFact]
    public async Task Die_Vorhersage_des_BAFU_kommt_aus_der_echten_Datei_nach_dem_letzten_Messwert_und_gilt_als_kontrolliert()
    {
        // Arrange: Datei ausgegeben 19.10.2026, 07:12 MEWZ, gemessen bis 06:00 MEWZ.
        var (_, site) = await PrepareRheinfelden();
        Sql.Sources.Overrides["locations=2091&"] = TestData.Read("hydro", "daterange-2091-flow-height.json");
        Sql.Sources.Overrides["pqprevi-official/Pqprevi_ICH2_Ctrl_2091.txt"] = TestData.Read("hydro", "pqprevi-official-ICH2_Ctrl-2091-20261019.txt");

        // Act
        var result = await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());

        // Assert
        var discharge = (await Discharge(site))!;
        Assert.Equal(1, result.ForecastStations);
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 6, 12, 0, TimeSpan.Zero), discharge.Forecast!.IssuedAt);
        Assert.True(discharge.Forecast.Official);
        Assert.Equal(new DischargeHour(new DateTimeOffset(2026, 10, 19, 15, 0, 0, TimeSpan.Zero), 326.2m), discharge.ForecastHours[0]);
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 23, 0, 0, TimeSpan.Zero), discharge.ForecastHours[^1].Hour);
        Assert.Equal(Enumerable.Range(19, 6).Select(d => new DateOnly(2026, 10, d)), discharge.Forecast.Days.Select(d => d.Date));
        Assert.Contains(Sql.RawData.Files.Keys, path => path.EndsWith("/bafu/official-ICH2_Ctrl-2091.txt", StringComparison.Ordinal));
    }

    [SqlFact]
    public async Task Ohne_kontrollierte_Vorhersage_gilt_die_automatisch_gerechnete_und_ist_so_gekennzeichnet()
    {
        // Arrange
        var (_, site) = await PrepareRheinfelden();
        Sql.Sources.Overrides["locations=2091&"] = TestData.Read("hydro", "daterange-2091-flow-height.json");
        Sql.Sources.Overrides["pqprevi-unofficial/Pqprevi_ICH2_Ctrl_2091.txt"] = TestData.Read("hydro", "pqprevi-official-ICH2_Ctrl-2091-20261019.txt");

        // Act
        await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());

        // Assert
        var discharge = (await Discharge(site))!;
        Assert.False(discharge.Forecast!.Official);
        Assert.Equal(326.2m, discharge.ForecastHours[0].Discharge);
    }

    [SqlFact]
    public async Task Die_Vorhersage_kommt_ab_8_Uhr_15_hoechstens_alle_3_Stunden_und_eine_neue_ersetzt_die_alte()
    {
        // Arrange: 08:00 Schweizer Zeit (06:00 UTC), noch vor der Ausgabe.
        var (_, site) = await PrepareRheinfelden();
        var file = TestData.Read("hydro", "pqprevi-official-ICH2_Ctrl-2091-20261019.txt");
        Sql.Sources.Overrides["locations=2091&"] = TestData.Read("hydro", "daterange-2091-flow-height.json");
        Sql.Sources.Overrides["pqprevi-official/Pqprevi_ICH2_Ctrl_2091.txt"] = file;
        Sql.Time.Now = new DateTimeOffset(2026, 10, 19, 6, 0, 0, TimeSpan.Zero);

        // Act
        var beforePublication = await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());
        Sql.Time.Now = new DateTimeOffset(2026, 10, 19, 6, 20, 0, TimeSpan.Zero);
        var first = await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());
        Sql.Time.Now = new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);
        var tooSoon = await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());
        Sql.Sources.Overrides["pqprevi-official/Pqprevi_ICH2_Ctrl_2091.txt"] = file.Replace("   326.2", "   400.0", StringComparison.Ordinal);
        Sql.Time.Now = new DateTimeOffset(2026, 10, 19, 9, 30, 0, TimeSpan.Zero);
        var second = await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());

        // Assert
        var forecast = (await Discharge(site))!.ForecastHours;
        Assert.Equal((0, 1, 0, 1), (beforePublication.ForecastStations, first.ForecastStations, tooSoon.ForecastStations, second.ForecastStations));
        Assert.Equal(400.0m, forecast.Single(h => h.Hour == new DateTimeOffset(2026, 10, 19, 15, 0, 0, TimeSpan.Zero)).Discharge);
        Assert.Equal(forecast.Count, forecast.Select(h => h.Hour).Distinct().Count());
    }

    [SqlFact]
    public async Task Die_Vorhersage_beginnt_mit_der_Stunde_nach_dem_letzten_Messwert_die_Zeile_hh_ist_das_Mittel_der_Stunde_bis_hh()
    {
        // Arrange: gemessen bis 06:00 MEWZ = 05:00 UTC. Zeile 06 (365.3) ist das gemessene Mittel 05–06 MEWZ, Zeile 07 (353.7) das erste vorhergesagte, 06–07 MEWZ.
        await PrepareRheinfelden();
        Sql.Sources.Overrides["locations=2091&"] = TestData.Read("hydro", "daterange-2091-flow-height.json");
        Sql.Sources.Overrides["pqprevi-official/Pqprevi_ICH2_Ctrl_2091.txt"] = TestData.Read("hydro", "pqprevi-official-ICH2_Ctrl-2091-20261019.txt");

        // Act
        await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());

        // Assert
        var first = await FirstForecastHour();
        Assert.Equal((new DateTimeOffset(2026, 10, 19, 5, 0, 0, TimeSpan.Zero), 353.7m), (first.Hour, first.Discharge));
    }

    [SqlFact]
    public async Task Eine_Vorhersage_von_gestern_zaehlt_nicht_und_der_naechste_Lauf_versucht_es_wieder()
    {
        // Arrange
        var (_, site) = await PrepareRheinfelden();
        var file = TestData.Read("hydro", "pqprevi-official-ICH2_Ctrl-2091-20261019.txt");
        Sql.Sources.Overrides["locations=2091&"] = TestData.Read("hydro", "daterange-2091-flow-height.json");
        Sql.Sources.Overrides["pqprevi-official/Pqprevi_ICH2_Ctrl_2091.txt"] = file.Replace("Emission:           19.10.2026", "Emission:           18.10.2026", StringComparison.Ordinal);

        // Act
        var yesterday = await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());
        Sql.Sources.Overrides["pqprevi-official/Pqprevi_ICH2_Ctrl_2091.txt"] = file;
        Sql.Time.Now = Now.AddMinutes(30);
        var retry = await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());

        // Assert
        Assert.Equal((0, 1), (yesterday.ForecastStations, retry.ForecastStations));
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 6, 12, 0, TimeSpan.Zero), (await Discharge(site))!.Forecast!.IssuedAt);
    }

    [SqlFact]
    public async Task Faellt_das_BAFU_aus_hoert_der_Abruf_nach_dem_ersten_Fehler_auf_und_der_naechste_Lauf_versucht_es_wieder()
    {
        // Arrange
        await PrepareRheinfelden();
        Sql.Sources.Overrides["locations=2091&"] = TestData.Read("hydro", "daterange-2091-flow-height.json");
        Sql.Sources.Statuses["pqprevi-"] = System.Net.HttpStatusCode.ServiceUnavailable;

        // Act
        var outage = await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());
        var duringOutage = Sql.Sources.Requests.Where(url => url.Contains("pqprevi-", StringComparison.Ordinal)).Distinct().ToList();
        Sql.Sources.Statuses.Clear();
        Sql.Sources.Overrides["pqprevi-official/Pqprevi_ICH2_Ctrl_2091.txt"] = TestData.Read("hydro", "pqprevi-official-ICH2_Ctrl-2091-20261019.txt");
        Sql.Time.Now = Now.AddMinutes(30);
        var after = await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());

        // Assert
        Assert.Equal((0, 1), (outage.ForecastStations, after.ForecastStations));
        Assert.Equal("https://www.hydrodata.ch/data/pqprevi-official/Pqprevi_ICH2_Ctrl_2091.txt", Assert.Single(duringOutage));
    }

    [SqlFact]
    public async Task Kommt_keine_neue_Vorhersage_faellt_eine_ueber_einen_Tag_alte_weg()
    {
        // Arrange
        var (_, site) = await PrepareRheinfelden();
        Sql.Sources.Overrides["locations=2091&"] = TestData.Read("hydro", "daterange-2091-flow-height.json");
        Sql.Sources.Overrides["pqprevi-official/Pqprevi_ICH2_Ctrl_2091.txt"] = TestData.Read("hydro", "pqprevi-official-ICH2_Ctrl-2091-20261019.txt");
        await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());
        Sql.Sources.Overrides.TryRemove("pqprevi-official/Pqprevi_ICH2_Ctrl_2091.txt", out _);

        // Act
        Sql.Time.Now = new DateTimeOffset(2026, 10, 20, 7, 0, 0, TimeSpan.Zero);
        await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());

        // Assert
        var discharge = (await Discharge(site))!;
        Assert.Null(discharge.Forecast);
        Assert.Empty(discharge.ForecastHours);
    }

    [SqlFact]
    public async Task Ohne_Zugangsdaten_holt_der_Import_keine_Vorhersage()
    {
        // Arrange
        await PrepareRheinfelden();
        Sql.Sources.Overrides["locations=2091&"] = TestData.Read("hydro", "daterange-2091-flow-height.json");
        var options = Sql.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Tauchplaetze.Core.Features.Forecasts.Sources.Hydro.BafuForecastOptions>>().Value;
        var password = options.Password;
        options.Password = null;

        // Act
        ImportDischargeResult result;
        try
        {
            result = await Command<ImportDischargeCommand, ImportDischargeResult>(new ImportDischargeCommand());
        }
        finally
        {
            options.Password = password;
        }

        // Assert
        Assert.Equal((1, 0), (result.Stations, result.ForecastStations));
        Assert.DoesNotContain(Sql.Sources.Requests, url => url.Contains("pqprevi-", StringComparison.Ordinal));
    }

    [SqlTheory]
    [InlineData("2091", true)]
    [InlineData(" 2091 ", true)]
    [InlineData("", true)]
    [InlineData("209", false)]
    [InlineData("20911", false)]
    [InlineData("2091&x=1", false)]
    [InlineData("abcd", false)]
    public async Task Als_Station_gilt_nur_eine_BAFU_Stationsnummer_mit_vier_Ziffern(string station, bool valid)
    {
        // Arrange
        var (moderator, site) = await PrepareRheinfelden();
        var edit = (await Query<GetDiveSiteForEditingQuery, DiveSiteForEditing?>(new GetDiveSiteForEditingQuery(site)))!;

        // Act
        var result = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, edit.BaseNumber,
            edit.Content with { InflowStationOverride = station }, null));

        // Assert
        Assert.Equal(valid ? [] : [EditorialError.InvalidStation], result.Errors);
    }

    private async Task<(string Moderator, Guid Site)> PrepareRheinfelden()
    {
        Sql.Time.Now = Now;
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var moderator = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        var rhein = (await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery("de"))).Single(w => w.Slug == "rhein").Id;
        var result = await Command<ProposeDiveSiteCommand, EditorialResult>(
            new ProposeDiveSiteCommand(moderator, "Strandbad", rhein, 47.55164, 7.7767, "Rheinfelden", "AG", null));
        return (moderator, result.DiveSiteId!.Value);
    }

    private async Task<(DateTimeOffset Hour, decimal Discharge)> FirstForecastHour()
    {
        (DateTimeOffset, decimal) first = default;
        await Sql.Arrange(async db => first = await db.HydroHours
            .Where(h => h.Kind == HydroSeriesKind.Forecast)
            .OrderBy(h => h.Hour)
            .Select(h => new ValueTuple<DateTimeOffset, decimal>(h.Hour, h.Discharge))
            .FirstAsync(TestContext.Current.CancellationToken));
        return first;
    }

    private Task<SiteDischarge?> Discharge(Guid site) => Query<GetSiteDischargeQuery, SiteDischarge?>(new GetSiteDischargeQuery(site));

    private async Task<int> HourCount()
    {
        var count = 0;
        await Sql.Arrange(async db => count = await db.HydroHours.CountAsync(TestContext.Current.CancellationToken));
        return count;
    }

    /// <summary>Eine Antwort von existenz.ch für die Station 2091 mit einem Wert alle 10 Minuten.</summary>
    private static string Flow(DateTimeOffset from, DateTimeOffset to, Func<DateTimeOffset, double> value)
    {
        var values = new StringBuilder();
        for (var time = from; time <= to; time = time.AddMinutes(10))
        {
            values.Append(values.Length > 0 ? "," : "")
                .Append(CultureInfo.InvariantCulture, $$"""{"timestamp":{{time.ToUnixTimeSeconds()}},"loc":"2091","par":"flow","val":{{value(time)}}}""");
        }

        return $$"""{"source":"BAFU","payload":[{{values}}]}""";
    }
}
