using Tauchplaetze.Spike.DataSources.Alplakes;
using Tauchplaetze.Spike.DataSources.Analysis;

namespace Tauchplaetze.Spike.DataSources.Tests;

public class SourceParsingTests
{
    // Shape of /simulations/depthtime: depth from bottom to surface, temperature rows per depth, one value per time step.
    private const string DepthTimeJson = """
        {
          "time": ["2026-10-17T00:00:00+00:00", "2026-10-17T03:00:00+00:00", "2026-10-17T06:00:00+00:00"],
          "lat": 47.2841, "lng": 8.6079,
          "depth": { "data": [30.0, 10.0, 0.5], "unit": "m", "description": "Distance from the surface" },
          "distance": { "data": 98.95, "unit": "m", "description": "Distance from requested location" },
          "variables": {
            "temperature": { "data": [[7.5, 7.5, 7.4], [18.5, null, 18.3], [18.7, 18.6, 18.5]], "unit": "degC", "description": "Water temperature" },
            "u": { "data": [[null, null, null], [null, null, null], [null, null, null]], "unit": "m/s", "description": "" }
          }
        }
        """;

    [Fact]
    public void ProfileAt_TimeBetweenSteps_UsesClosestStepAndSortsFromSurface()
    {
        // Arrange
        var series = DepthTimeSeries.Parse(DepthTimeJson);

        // Act
        var profile = series.ProfileAt(DateTimeOffset.Parse("2026-10-17T05:00:00+00:00"));

        // Assert
        profile.Time.ShouldBe(DateTimeOffset.Parse("2026-10-17T06:00:00+00:00"));
        profile.Points.Select(p => p.Depth).ShouldBe([0.5, 10.0, 30.0]);
        profile.Points.Select(p => p.Temperature).ShouldBe([18.5, 18.3, 7.4]);
        series.DistanceToGridPoint.ShouldBe(98.95);
    }

    [Fact]
    public void ProfileAt_StepWithMissingValue_SkipsThatDepth()
    {
        // Arrange
        var series = DepthTimeSeries.Parse(DepthTimeJson);

        // Act
        var profile = series.ProfileAt(DateTimeOffset.Parse("2026-10-17T03:00:00+00:00"));

        // Assert
        profile.Points.Select(p => p.Depth).ShouldBe([0.5, 30.0]);
    }

    [Fact]
    public void ParseList_ValidPixelsAsPercentText_ReadsNumberAndSortsNewestFirst()
    {
        // Arrange
        const string json = """
            [
              { "time": "2026-10-05T09:58:26+00:00", "full_tile": "a.tif", "lake_subset": "a_zurich.tif", "valid_pixels": "38%" },
              { "time": "2026-10-12T10:17:07+00:00", "full_tile": "b.tif", "lake_subset": "b_zurich.tif", "valid_pixels": 16 }
            ]
            """;

        // Act
        var images = RemoteSensingImage.ParseList(json);

        // Assert
        images.Select(i => i.ValidPixels).ShouldBe([16d, 38d]);
    }

    [Fact]
    public void ParseRemoteSensingDirectory_UnknownLake_ReturnsEmpty()
    {
        // Arrange
        const string json = """{ "zurich": { "sentinel3": ["chla", "Zsd_lee"], "collection": ["ST"] } }""";

        // Act
        var known = AlplakesClient.ParseRemoteSensingDirectory(json, "zurich");
        var unknown = AlplakesClient.ParseRemoteSensingDirectory(json, "walensee");

        // Assert
        known["sentinel3"].ShouldBe(["chla", "Zsd_lee"]);
        unknown.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0.5, 0)]
    [InlineData(5.4, 1)]
    [InlineData(11.5, 3)]
    [InlineData(28.4, 4)]
    [InlineData(28.5, 5)]
    [InlineData(130, 12)]
    public void FromKmh_SpeedAtScaleLimits_GivesWmoBeaufort(double speedKmh, int expected)
    {
        // Act
        var beaufort = Beaufort.FromKmh(speedKmh);

        // Assert
        beaufort.ShouldBe(expected);
    }
}
