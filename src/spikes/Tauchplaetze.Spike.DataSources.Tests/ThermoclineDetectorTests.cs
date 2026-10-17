using Tauchplaetze.Spike.DataSources.Analysis;

namespace Tauchplaetze.Spike.DataSources.Tests;

public class ThermoclineDetectorTests
{
    // Delft3D-FLOW profile at Riff Williams, 2026-10-17 00:00 UTC (subset of the levels).
    private static readonly TemperatureProfile AutumnProfile = Profile(
        (0.05, 18.76), (2.25, 18.83), (5.5, 18.69), (8.125, 18.60), (10.125, 18.56), (11.125, 18.51), (12.125, 18.36),
        (12.75, 18.16), (13.75, 17.64), (14.75, 16.98), (15.875, 16.17), (16.875, 15.47), (17.875, 14.76),
        (18.5, 14.39), (19.5, 13.74), (20.5, 13.11), (21.5, 12.49), (22.5, 11.89), (23.5, 11.31), (24.5, 10.74),
        (25.5, 10.23), (27.0, 9.42), (29.0, 8.59), (32.5, 7.46), (35.5, 7.14), (38.0, 6.94), (42.5, 6.66));

    [Fact]
    public void Detect_StratifiedAutumnProfile_FindsLayerBetween13And27Metres()
    {
        // Arrange
        const double threshold = 0.5;

        // Act
        var thermocline = ThermoclineDetector.Detect(AutumnProfile, threshold);

        // Assert
        thermocline.ShouldNotBeNull();
        thermocline.Top.ShouldBe(12.75);
        thermocline.Bottom.ShouldBe(27.0);
        thermocline.MaxGradient.ShouldBeGreaterThan(0.6);
    }

    [Fact]
    public void Detect_MixedWinterProfile_ReturnsNull()
    {
        // Arrange
        var winter = Profile((0.5, 5.4), (5, 5.3), (10, 5.2), (20, 5.0), (30, 4.9), (40, 4.8));

        // Act
        var thermocline = ThermoclineDetector.Detect(winter, 0.5);

        // Assert
        thermocline.ShouldBeNull();
    }

    [Fact]
    public void Detect_HigherThreshold_GivesThinnerLayer()
    {
        // Arrange
        var lowThreshold = ThermoclineDetector.Detect(AutumnProfile, 0.5)!;

        // Act
        var highThreshold = ThermoclineDetector.Detect(AutumnProfile, 0.6)!;

        // Assert
        highThreshold.Top.ShouldBeGreaterThanOrEqualTo(lowThreshold.Top);
        highThreshold.Bottom.ShouldBeLessThanOrEqualTo(lowThreshold.Bottom);
    }

    [Theory]
    [InlineData(10, 18.56)]
    [InlineData(20, 13.425)]
    [InlineData(30, 8.27714)]
    public void TemperatureAt_DepthBetweenLevels_InterpolatesLinearly(double depth, double expected)
    {
        // Act
        var temperature = AutumnProfile.TemperatureAt(depth);

        // Assert
        temperature.ShouldNotBeNull();
        temperature.Value.ShouldBe(expected, 0.01);
    }

    [Fact]
    public void TemperatureAt_BelowDeepestLevel_ReturnsNull()
    {
        // Act
        var temperature = AutumnProfile.TemperatureAt(60);

        // Assert
        temperature.ShouldBeNull();
    }

    private static TemperatureProfile Profile(params (double Depth, double Temperature)[] points) =>
        new(DateTimeOffset.UnixEpoch, points.Select(p => new ProfilePoint(p.Depth, p.Temperature)).ToList());
}
