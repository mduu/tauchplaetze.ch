using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Queries;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

public class ForecastRunStateTests(SqlServer sql) : SqlTest(sql)
{
    /// <summary>Samstag 17.10.2026, 07:00 Schweizer Zeit. Der nächste geplante Lauf ist um 13:00.</summary>
    private static readonly DateTimeOffset Morning = new(2026, 10, 17, 5, 0, 0, TimeSpan.Zero);

    [SqlTheory]
    [InlineData(10, true)]
    [InlineData(150, false)]
    public async Task Ein_Lauf_gilt_nur_als_laufend_solange_er_juenger_als_2_Stunden_ist(int minutesAgo, bool running)
    {
        // Arrange
        Sql.Time.Now = Morning;
        await Arrange(Run(Morning.AddMinutes(-minutesAgo), ForecastRunStatus.Running));

        // Act
        var state = await Query<GetForecastRunStateQuery, ForecastRunState>(new GetForecastRunStateQuery());

        // Assert
        Assert.Equal(running, state.Running);
    }

    [SqlFact]
    public async Task Ein_abgebrochener_Lauf_von_heute_wird_nachgeholt()
    {
        // Arrange
        Sql.Time.Now = Morning;
        await Arrange(Run(Morning.AddHours(-2), ForecastRunStatus.Aborted));

        // Act
        var state = await Query<GetForecastRunStateQuery, ForecastRunState>(new GetForecastRunStateQuery());

        // Assert
        Assert.False(state.Running);
        Assert.True(state.ResumeAborted);
    }

    [SqlFact]
    public async Task Kam_nach_dem_Abbruch_ein_Lauf_durch_wird_nichts_nachgeholt()
    {
        // Arrange
        Sql.Time.Now = Morning;
        await Arrange(
            Run(Morning.AddHours(-3), ForecastRunStatus.Aborted),
            Run(Morning.AddHours(-2), ForecastRunStatus.Succeeded));

        // Act
        var state = await Query<GetForecastRunStateQuery, ForecastRunState>(new GetForecastRunStateQuery());

        // Assert
        Assert.False(state.ResumeAborted);
    }

    [SqlFact]
    public async Task Kommt_der_naechste_geplante_Lauf_bald_wird_nichts_nachgeholt()
    {
        // Arrange
        var shortlyBeforeOne = new DateTimeOffset(2026, 10, 17, 10, 30, 0, TimeSpan.Zero);
        Sql.Time.Now = shortlyBeforeOne;
        await Arrange(Run(shortlyBeforeOne.AddHours(-3), ForecastRunStatus.Aborted));

        // Act
        var state = await Query<GetForecastRunStateQuery, ForecastRunState>(new GetForecastRunStateQuery());

        // Assert
        Assert.False(state.ResumeAborted);
    }

    [SqlFact]
    public async Task Ein_abgebrochener_Lauf_von_gestern_wird_nicht_nachgeholt()
    {
        // Arrange
        Sql.Time.Now = Morning;
        await Arrange(Run(Morning.AddDays(-1), ForecastRunStatus.Aborted));

        // Act
        var state = await Query<GetForecastRunStateQuery, ForecastRunState>(new GetForecastRunStateQuery());

        // Assert
        Assert.False(state.ResumeAborted);
    }

    private Task Arrange(params ForecastRun[] runs) =>
        Sql.Arrange(db =>
        {
            db.ForecastRuns.AddRange(runs);
            return Task.CompletedTask;
        });

    private static ForecastRun Run(DateTimeOffset startedAt, ForecastRunStatus status) => new()
    {
        Id = Guid.CreateVersion7(startedAt),
        StartedAt = startedAt,
        FinishedAt = status == ForecastRunStatus.Running ? null : startedAt.AddMinutes(20),
        Status = status,
        SourceStatus = "[]",
        RawDataPath = "x",
    };
}
