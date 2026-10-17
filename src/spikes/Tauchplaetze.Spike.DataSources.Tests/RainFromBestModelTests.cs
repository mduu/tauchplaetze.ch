using Tauchplaetze.Spike.DataSources.Weather;

namespace Tauchplaetze.Spike.DataSources.Tests;

public class RainFromBestModelTests
{
    private static readonly DateTime Start = new(2026, 10, 18, 0, 0, 0);

    [Fact]
    public void RainFromBestModel_FinerModelEndsEarly_FillsMissingHoursFromNextModel()
    {
        // Arrange
        var fine = Model("fine", hours: 20, rainPerHour: 0.5);
        var coarse = Model("coarse", hours: 24, rainPerHour: 0.1);

        // Act
        var rain = DataSourceCheck.RainFromBestModel([fine, coarse], Start, Start.AddDays(1));

        // Assert
        rain.ShouldBe(20 * 0.5 + 4 * 0.1);
    }

    [Fact]
    public void RainFromBestModel_HourMissingInAllModels_ReturnsNull()
    {
        // Arrange
        var fine = Model("fine", hours: 20, rainPerHour: 0.5);

        // Act
        var rain = DataSourceCheck.RainFromBestModel([fine], Start, Start.AddDays(1));

        // Assert
        rain.ShouldBeNull();
    }

    private static DataSourceCheck.ModelWeather Model(string name, int hours, double rainPerHour) =>
        new(name, Enumerable.Range(0, hours)
            .Select(h => new WeatherHour(Start.AddHours(h), 5, 180, 10, rainPerHour, 12))
            .ToList());
}
