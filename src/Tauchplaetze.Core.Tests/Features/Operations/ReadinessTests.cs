using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Operations.Queries;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Operations;

public class ReadinessTests(SqlServer sql) : SqlTest(sql)
{
    [SqlTheory]
    [InlineData(29, ForecastRunStatus.Succeeded, true)]
    [InlineData(29, ForecastRunStatus.PartiallySucceeded, true)]
    [InlineData(31, ForecastRunStatus.Succeeded, false)]
    [InlineData(1, ForecastRunStatus.Failed, false)]
    public async Task Bereit_ist_die_App_nur_mit_einem_brauchbaren_Lauf_der_hoechstens_30_Stunden_alt_ist(int hoursAgo, ForecastRunStatus status, bool ready)
    {
        // Arrange
        var startedAt = Sql.Time.GetUtcNow().AddHours(-hoursAgo);
        await Sql.Arrange(db =>
        {
            db.ForecastRuns.Add(new ForecastRun
            {
                Id = Guid.CreateVersion7(startedAt),
                StartedAt = startedAt,
                FinishedAt = startedAt.AddMinutes(20),
                Status = status,
                RawDataPath = "x",
            });
            return Task.CompletedTask;
        });

        // Act
        var readiness = await Query<GetReadinessQuery, Readiness>(new GetReadinessQuery());

        // Assert
        Assert.Equal(ready, readiness.IsReady);
    }

    [SqlFact]
    public async Task Ohne_Lauf_ist_die_App_nicht_bereit()
    {
        // Act
        var readiness = await Query<GetReadinessQuery, Readiness>(new GetReadinessQuery());

        // Assert
        Assert.False(readiness.IsReady);
        Assert.Equal("no forecast run yet", readiness.Problem);
    }
}
