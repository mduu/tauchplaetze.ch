using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Commands;
using Tauchplaetze.Core.Features.Forecasts.Queries;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Features.WaterBodies.Queries;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

/// <remarks>
/// Der Platz liegt in Erlenbach am Zürichsee, näher bei der Station 2209 Zürich als bei 2014 Schmerikon. Die Uhr steht auf dem 21.11.2026, 07:15 UTC.
/// Echte Antworten von diesem Morgen: Pegel 405.55 m ü. M., Statistik für den 21.11.: 5. Perzentil 405.845, Median 405.924, 95. Perzentil 406.007.
/// </remarks>
public class LakeLevelTests(SqlServer sql) : SqlTest(sql)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 7, 15, 0, TimeSpan.Zero);

    [SqlFact]
    public async Task Liegt_der_Pegel_unter_dem_5_Perzentil_des_Kalendertags_ist_er_sehr_tief_mit_Abstand_zum_Median()
    {
        // Arrange
        var site = await PrepareErlenbach();

        // Act
        var result = await Command<ImportLakeLevelsCommand, ImportLakeLevelsResult>(new ImportLakeLevelsCommand());

        // Assert
        var level = await LakeLevel(site);
        Assert.Equal((1, 0, 1), (result.Stations, result.StationsWithoutValues, result.Norms));
        Assert.Equal(("2209", 405.55m, LakeLevelDeviation.VeryLow, -0.374m), (level!.StationId, level.Level, level.Deviation, level.FromMedian));
        Assert.Equal(new DateTimeOffset(2026, 11, 21, 7, 0, 0, TimeSpan.Zero), level.MeasuredAt);
        Assert.DoesNotContain(Sql.Sources.Requests, url => url.Contains("2014", StringComparison.Ordinal));
    }

    [SqlTheory]
    [InlineData(405.90, null)]
    [InlineData(406.10, null)]
    [InlineData(406.30, LakeLevelDeviation.VeryHigh)]
    public async Task Einen_Hinweis_gibt_es_erst_ausserhalb_des_ueblichen_Bereichs_und_ab_30_cm_Abstand_zum_Median(double value, LakeLevelDeviation? expected)
    {
        // Arrange
        var site = await PrepareErlenbach();
        Sql.Sources.Overrides["parameters=height"] = Height(value);

        // Act
        await Command<ImportLakeLevelsCommand, ImportLakeLevelsResult>(new ImportLakeLevelsCommand());

        // Assert
        Assert.Equal(expected, (await LakeLevel(site))!.Deviation);
    }

    [SqlFact]
    public async Task Die_Statistik_kommt_einmal_pro_Tag_und_ein_Messwert_aelter_als_24_Stunden_zeigt_nichts()
    {
        // Arrange
        var site = await PrepareErlenbach();
        await Command<ImportLakeLevelsCommand, ImportLakeLevelsResult>(new ImportLakeLevelsCommand());
        Sql.Time.Now = Now.AddHours(1);

        // Act
        var second = await Command<ImportLakeLevelsCommand, ImportLakeLevelsResult>(new ImportLakeLevelsCommand());
        Sql.Time.Now = Now.AddHours(25);
        var stale = await LakeLevel(site);

        // Assert
        Assert.Equal(0, second.Norms);
        Assert.Single(Sql.Sources.Requests, url => url.Contains("p_annual", StringComparison.Ordinal));
        Assert.Null(stale);
    }

    [SqlFact]
    public async Task Faellt_das_BAFU_aus_holt_der_naechste_Lauf_die_Statistik_nach()
    {
        // Arrange
        var site = await PrepareErlenbach();
        Sql.Sources.Statuses["p_annual"] = System.Net.HttpStatusCode.ServiceUnavailable;
        var outage = await Command<ImportLakeLevelsCommand, ImportLakeLevelsResult>(new ImportLakeLevelsCommand());
        Sql.Sources.Statuses.Clear();
        Sql.Time.Now = Now.AddHours(1);

        // Act
        var retry = await Command<ImportLakeLevelsCommand, ImportLakeLevelsResult>(new ImportLakeLevelsCommand());

        // Assert
        Assert.Equal((0, 1), (outage.Norms, retry.Norms));
        Assert.Equal(LakeLevelDeviation.VeryLow, (await LakeLevel(site))!.Deviation);
    }

    [SqlTheory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public async Task Eine_Statistik_gilt_hoechstens_3_Tage_weiter_wenn_keine_neue_kommt(int days, bool shown)
    {
        // Arrange: Danach liefert das BAFU keine Statistik mehr (404), der Pegel kommt weiter.
        var site = await PrepareErlenbach();
        await Command<ImportLakeLevelsCommand, ImportLakeLevelsResult>(new ImportLakeLevelsCommand());
        Sql.Sources.Overrides.TryRemove("p_annual/2209_", out _);
        Sql.Time.Now = Now.AddDays(days);
        Sql.Sources.Overrides["parameters=height"] = Height(405.55, Sql.Time.Now);

        // Act
        await Command<ImportLakeLevelsCommand, ImportLakeLevelsResult>(new ImportLakeLevelsCommand());

        // Assert
        Assert.Equal(shown, await LakeLevel(site) is not null);
    }

    private async Task<Guid> PrepareErlenbach()
    {
        Sql.Time.Now = Now;
        Sql.Sources.Overrides["parameters=height"] = TestData.Read("hydro", "daterange-2014-2209-height.json");
        Sql.Sources.Overrides["p_annual/2209_"] = TestData.Read("hydro", "p_annual-2209-20261121.json");
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var moderator = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        var lake = (await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery("de"))).Single(w => w.Slug == "zuerichsee").Id;
        var result = await Command<ProposeDiveSiteCommand, EditorialResult>(
            new ProposeDiveSiteCommand(moderator, "Erlenbach", lake, 47.2849, 8.6085, "Erlenbach", "ZH", null));
        return result.DiveSiteId!.Value;
    }

    private Task<SiteLakeLevel?> LakeLevel(Guid site) => Query<GetSiteLakeLevelQuery, SiteLakeLevel?>(new GetSiteLakeLevelQuery(site));

    /// <summary>Eine Antwort von existenz.ch für die Station 2209 mit einem Wert 15 Minuten vor <paramref name="now"/> (ohne: 07:00 UTC am 21.11.2026).</summary>
    private static string Height(double value, DateTimeOffset? now = null) =>
        $$"""{"source":"BAFU","payload":[{"timestamp":{{(now ?? Now).AddMinutes(-15).ToUnixTimeSeconds()}},"loc":"2209","par":"height","val":{{value.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}]}""";
}
