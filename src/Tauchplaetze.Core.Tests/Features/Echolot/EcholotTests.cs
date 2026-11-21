using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Echolot;
using Tauchplaetze.Core.Features.Echolot.Queries;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.Editorial.Queries;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Features.WaterBodies.Queries;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Echolot;

/// <remarks>Die Uhr startet am 01.10.2026, 10:00 UTC. Die Neuigkeiten aus `news.json` sind jünger und stehen darum zuoberst.</remarks>
public class EcholotTests(SqlServer sql) : SqlTest(sql)
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);

    [SqlFact]
    public async Task Ein_Platz_erscheint_am_Tag_seines_ersten_Texts_Bewertungen_und_spaetere_Aenderungen_nicht()
    {
        // Arrange
        var (moderator, site) = await Prepare("Riff Williams");
        Sql.Time.Now = Start.AddDays(1);
        await Change(moderator, site, c => c with { DifficultyRating = 3 });
        Sql.Time.Now = Start.AddDays(2);
        await Change(moderator, site, c => c with { Translations = [Text("de", "Steilwand bis 40 m.")] });
        Sql.Time.Now = Start.AddDays(5);
        await Change(moderator, site, c => c with { Translations = [Text("de", "Steilwand bis 45 m.")] });

        // Act
        var echolot = await Echolot();

        // Assert
        var described = Assert.Single(echolot.Entries, e => e.Kind == EcholotEntryKind.Described);
        Assert.Equal((new DateOnly(2026, 10, 3), "Riff Williams", "Zürichsee"), (described.Date, described.Sites[0].Name, described.Sites[0].WaterBodyName));
    }

    [SqlFact]
    public async Task Der_Backfill_nimmt_die_Freigabe_der_ersten_Version_mit_Text_und_ein_zweiter_Lauf_aendert_nichts()
    {
        // Arrange
        var (moderator, site) = await Prepare("Riff Williams");
        Sql.Time.Now = Start.AddDays(3);
        await Change(moderator, site, c => c with { Translations = [Text("de", "Steilwand bis 40 m.")] });
        await Sql.Arrange(db => db.DiveSites.Where(s => s.Id == site).ExecuteUpdateAsync(u => u.SetProperty(s => s.FirstDescribedAt, (DateTimeOffset?)null)));
        Sql.Time.Now = Start.AddDays(20);

        // Act
        var first = await Command<BackfillFirstDescribedCommand, BackfillFirstDescribedResult>(new BackfillFirstDescribedCommand());
        var second = await Command<BackfillFirstDescribedCommand, BackfillFirstDescribedResult>(new BackfillFirstDescribedCommand());

        // Assert
        Assert.Equal((1, 0), (first.Sites, second.Sites));
        Assert.Equal(new DateOnly(2026, 10, 4), Assert.Single((await Echolot()).Entries, e => e.Kind == EcholotEntryKind.Described).Date);
    }

    [SqlFact]
    public async Task Es_erscheinen_die_letzten_20_Neuigkeiten_Plaetze_eines_Tages_als_ein_Eintrag()
    {
        // Arrange: zwei Funktionen aus news.json, dazu 20 Tage mit je einem Platz und ein Tag mit zwei Plätzen.
        var (moderator, first) = await Prepare("Platz 0");
        var sites = new List<Guid> { first };
        for (var i = 1; i <= 21; i++)
        {
            sites.Add(await Propose(moderator, $"Platz {i}"));
        }

        await Sql.Arrange(async db =>
        {
            for (var i = 0; i < sites.Count; i++)
            {
                var describedAt = Start.AddDays(Math.Min(i, 20));
                await db.DiveSites.Where(s => s.Id == sites[i]).ExecuteUpdateAsync(u => u.SetProperty(s => s.FirstDescribedAt, describedAt));
            }
        });

        // Act
        var echolot = await Echolot();

        // Assert
        Assert.Equal(20, echolot.Entries.Count);
        Assert.True(echolot.HasOlderEntries);
        Assert.Equal([EcholotEntryKind.Feature, EcholotEntryKind.Feature, EcholotEntryKind.Described], echolot.Entries.Take(3).Select(e => e.Kind));
        Assert.Equal(["Platz 20", "Platz 21"], echolot.Entries[2].Sites.Select(s => s.Name));
    }

    [SqlFact]
    public async Task Jede_Quelle_ist_aktuell_verspaetet_gestoert_oder_ohne_Daten()
    {
        // Arrange: Lauf vor 10 Stunden. Alplakes ein Call gescheitert, Open-Meteo alle, Satellit vor 7 Tagen. Abfluss vor 30 Minuten, kein Pegel.
        var (_, site) = await Prepare("Riff Williams");
        var now = Start.AddDays(10);
        Sql.Time.Now = now;
        var finished = now.AddHours(-10);
        await Sql.Arrange(db =>
        {
            db.ForecastRuns.Add(new ForecastRun
            {
                Id = Guid.CreateVersion7(finished),
                StartedAt = finished.AddMinutes(-20),
                FinishedAt = finished,
                Status = ForecastRunStatus.PartiallySucceeded,
                Sites = 318,
                RawDataPath = "x",
                SourceStatus = """
                    [{"source":"alplakes","key":"3d-a","ok":true},{"source":"alplakes","key":"1d-b","ok":false},
                     {"source":"alplakes","key":"satellite-c","ok":true},{"source":"open-meteo","key":"d","ok":false}]
                    """,
            });
            db.DailyForecasts.Add(new DailyForecast { DiveSiteId = site, Date = new DateOnly(2026, 10, 11), SecchiObservedAt = new DateOnly(2026, 10, 4), ModelVersion = "x" });
            db.HydroReadings.Add(new HydroReading { StationId = "2091", MeasuredAt = now.AddMinutes(-30), Discharge = 300, ImportedAt = now });
            return Task.CompletedTask;
        });

        // Act
        var sources = (await Echolot()).Sources.ToDictionary(s => s.Source, s => s.Health);

        // Assert
        Assert.Equal(DataHealth.Current, sources[DataSource.Forecast]);
        Assert.Equal(DataHealth.Late, sources[DataSource.LakeModels]);
        Assert.Equal(DataHealth.Down, sources[DataSource.Weather]);
        Assert.Equal(DataHealth.Late, sources[DataSource.Satellite]);
        Assert.Equal(DataHealth.Current, sources[DataSource.Discharge]);
        Assert.Equal(DataHealth.Unknown, sources[DataSource.LakeLevel]);
    }

    private async Task<(string Moderator, Guid Site)> Prepare(string name)
    {
        Sql.Time.Now = Start;
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var moderator = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        return (moderator, await Propose(moderator, name));
    }

    private async Task<Guid> Propose(string moderator, string name)
    {
        var lake = (await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery("de"))).Single(w => w.Slug == "zuerichsee").Id;
        var result = await Command<ProposeDiveSiteCommand, EditorialResult>(new ProposeDiveSiteCommand(moderator, name, lake, 47.2849, 8.6085, "Herrliberg", "ZH", null));
        return result.DiveSiteId!.Value;
    }

    private async Task Change(string moderator, Guid site, Func<DiveSiteContent, DiveSiteContent> change)
    {
        var edit = (await Query<GetDiveSiteForEditingQuery, DiveSiteForEditing?>(new GetDiveSiteForEditingQuery(site)))!;
        var result = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, edit.BaseNumber, change(edit.Content), null));
        Assert.Empty(result.Errors);
    }

    private static DiveSiteContentTranslation Text(string culture, string underwater) => new(culture, underwater, null, null, null, null, null, false);

    private Task<EcholotReport> Echolot() => Query<GetEcholotQuery, EcholotReport>(new GetEcholotQuery("de"));
}
