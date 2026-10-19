using Tauchplaetze.Core.Features.Forecasts;

namespace Tauchplaetze.Web.Tests;

public class ForecastScheduleTests
{
    [Theory]
    [InlineData("2026-10-18T01:00:00Z", "2026-10-18T02:30:00Z")]
    [InlineData("2026-10-18T08:00:00Z", "2026-10-18T11:00:00Z")]
    [InlineData("2026-10-18T11:00:00Z", "2026-10-19T02:30:00Z")]
    [InlineData("2026-10-24T12:00:00Z", "2026-10-25T03:30:00Z")]
    public void Der_naechste_Lauf_ist_um_04_30_oder_13_00_Uhr_Lokalzeit_auch_nach_der_Zeitumstellung(string now, string expected)
    {
        // Arrange
        var schedule = new ForecastSchedule();

        // Act
        var next = schedule.NextRun(DateTimeOffset.Parse(now));

        // Assert
        Assert.Equal(DateTimeOffset.Parse(expected), next);
    }
}
