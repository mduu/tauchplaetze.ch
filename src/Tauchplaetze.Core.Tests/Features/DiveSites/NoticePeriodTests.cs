using System.Globalization;
using Tauchplaetze.Core.Features.DiveSites;

namespace Tauchplaetze.Core.Tests.Features.DiveSites;

public class NoticePeriodTests
{
    [Theory]
    [InlineData("2026-10-15", "2026-12-15", false, "2026-11-01", NoticeTiming.Today)]
    [InlineData("2026-10-15", "2026-12-15", false, "2026-10-01", NoticeTiming.Soon)]
    [InlineData("2026-10-15", "2026-12-15", false, "2026-09-30", NoticeTiming.Later)]
    [InlineData("2026-10-15", "2026-12-15", false, "2026-12-16", null)]
    [InlineData(null, "2026-12-15", false, "2020-01-01", NoticeTiming.Today)]
    [InlineData("2026-10-15", null, false, "2030-01-01", NoticeTiming.Today)]
    [InlineData("2020-10-15", "2020-12-15", true, "2031-11-30", NoticeTiming.Today)]
    [InlineData("2020-10-15", "2020-12-15", true, "2031-12-16", NoticeTiming.Later)]
    [InlineData("2020-10-15", "2020-12-15", true, "2031-10-02", NoticeTiming.Soon)]
    [InlineData("2020-11-01", "2021-03-31", true, "2031-01-10", NoticeTiming.Today)]
    [InlineData("2020-11-01", "2021-03-31", true, "2031-04-01", NoticeTiming.Later)]
    [InlineData("2020-02-29", "2020-03-10", true, "2027-02-28", NoticeTiming.Today)]
    public void Ein_Hinweis_erscheint_gross_solange_er_gilt_klein_davor_und_jedes_Jahr_wieder(string? from, string? until, bool yearly, string day, NoticeTiming? expected)
    {
        // Arrange
        var period = new NoticePeriod(Date(from), Date(until), yearly);

        // Act
        var timing = period.Timing(Date(day)!.Value);

        // Assert
        Assert.Equal(expected, timing);
    }

    [Fact]
    public void Jedes_Jahr_ueber_den_Jahreswechsel_nennt_den_laufenden_Zeitraum_mit_Jahr()
    {
        // Arrange
        var period = new NoticePeriod(new DateOnly(2020, 11, 1), new DateOnly(2021, 3, 31), Yearly: true);

        // Act
        var next = period.Next(new DateOnly(2031, 1, 10));

        // Assert
        Assert.Equal((new DateOnly(2030, 11, 1), new DateOnly(2031, 3, 31)), next!.Value);
    }

    private static DateOnly? Date(string? value) => value is null ? null : DateOnly.Parse(value, CultureInfo.InvariantCulture);
}
