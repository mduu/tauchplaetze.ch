using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Queries;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

public class ForecastRunOverviewTests(SqlServer sql) : SqlTest(sql)
{
    private const string Failures = """
        [
          {"source":"alplakes","key":"3d-zurich-riff","ok":false,"statusCode":null,"error":"The operation didn't complete within the allowed timeout of '00:01:00'.","durationMs":60000},
          {"source":"alplakes","key":"3d-zurich-ermitage","ok":false,"statusCode":null,"error":"The operation didn't complete within the allowed timeout of '00:01:00'.","durationMs":60000},
          {"source":"alplakes","key":"3d-zurich-riff","ok":false,"statusCode":null,"error":"The request was canceled due to the configured HttpClient.Timeout of 240 seconds elapsing.","durationMs":240000},
          {"source":"open-meteo","key":"icon-a","ok":false,"statusCode":503,"error":"busy","durationMs":200},
          {"source":"open-meteo","key":"icon-b","ok":false,"statusCode":503,"error":"busy","durationMs":200},
          {"source":"alplakes","key":"satellite-zurich","ok":false,"statusCode":404,"error":"not found","durationMs":100},
          {"source":"open-meteo","key":"icon-c","ok":true,"statusCode":200,"error":null,"durationMs":100}
        ]
        """;

    [SqlFact]
    public async Task Die_Uebersicht_zieht_die_Bilanz_der_letzten_7_Tage_gruppiert_die_Fehler_und_filtert_Laeufe_mit_Fehlern()
    {
        // Arrange
        var admin = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        var now = FixedTime.SpikeDay;
        await Sql.Arrange(db =>
        {
            db.ForecastRuns.AddRange(
                Run(now.AddHours(-1), 10, ForecastRunStatus.Succeeded),
                Run(now.AddDays(-1), 20, ForecastRunStatus.PartiallySucceeded, Failures),
                Run(now.AddDays(-3), 5, ForecastRunStatus.Failed),
                Run(now.AddDays(-10), 30, ForecastRunStatus.Succeeded));
            return Task.CompletedTask;
        });

        // Act
        var all = (await Query<GetForecastRunOverviewQuery, ForecastRunOverview?>(new GetForecastRunOverviewQuery(admin)))!;
        var withErrors = (await Query<GetForecastRunOverviewQuery, ForecastRunOverview?>(new GetForecastRunOverviewQuery(admin, OnlyWithErrors: true)))!;

        // Assert
        Assert.Equal(new RunBalance(3, 1, 1, 1), all.Week);
        Assert.Equal(TimeSpan.FromSeconds(700), all.AverageDuration);
        Assert.Equal(now.AddHours(-1), all.LastRun!.StartedAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 17, 11, 0, 0, TimeSpan.Zero), all.NextRun);
        Assert.Equal(4, all.TotalRuns);
        Assert.Equal(2, withErrors.TotalRuns);
        var partial = withErrors.Runs[0];
        Assert.Equal((7, 6), (partial.Calls, partial.FailedCalls));
        Assert.Equal(
            [("alplakes", FailureKind.Timeout, (int?)null, 3), ("open-meteo", FailureKind.Http, 503, 2), ("alplakes", FailureKind.Http, 404, 1)],
            partial.Failures.Select(f => (f.Source, f.Kind, f.StatusCode, f.Count)));
        Assert.Equal(["3d-zurich-ermitage", "3d-zurich-riff"], partial.Failures[0].Keys);
        Assert.Equal(2, partial.Failures[0].Messages.Count);
    }

    [SqlFact]
    public async Task Die_Uebersicht_der_Laeufe_sehen_nur_Admins()
    {
        // Arrange
        await Command<RegisterCommand, AccountResult>(new RegisterCommand("anna@example.org", "Felswand-auf-25m", "Anna", "de", "/c?user={user}&token={token}"));
        var anna = Uri.UnescapeDataString(new Uri(Sql.Mail.LinkTo("anna@example.org")).Query.TrimStart('?').Split('&')[0]["user=".Length..]);

        // Act
        var overview = await Query<GetForecastRunOverviewQuery, ForecastRunOverview?>(new GetForecastRunOverviewQuery(anna));

        // Assert
        Assert.Null(overview);
    }

    private static ForecastRun Run(DateTimeOffset startedAt, int minutes, ForecastRunStatus status, string calls = "[]") => new()
    {
        Id = Guid.CreateVersion7(startedAt),
        StartedAt = startedAt,
        FinishedAt = startedAt.AddMinutes(minutes),
        Status = status,
        SourceStatus = calls,
        RawDataPath = "x",
    };
}
