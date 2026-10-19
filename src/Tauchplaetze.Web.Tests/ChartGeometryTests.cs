using System.Globalization;
using System.Text.RegularExpressions;
using Tauchplaetze.Web.Features.Forecasts;

namespace Tauchplaetze.Web.Tests;

public class ChartGeometryTests
{
    [Fact]
    public void Die_geglaettete_Linie_geht_durch_jeden_Punkt_bleibt_zwischen_gleichen_Werten_flach_und_schiesst_nie_ueber()
    {
        // Arrange
        ChartGeometry.Point[] points = [new(0, 50), new(100, 10), new(200, 10), new(300, 90), new(400, 85)];

        // Act
        var path = ChartGeometry.SmoothPath(points);

        // Assert
        var numbers = Regex.Matches(path, @"-?\d+(\.\d+)?").Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
        var ys = numbers.Where((_, i) => i % 2 == 1).ToList();
        Assert.All(ys, y => Assert.InRange(y, 10, 90));
        Assert.All(points.Skip(1), p => Assert.Contains($"{ChartGeometry.Number(p.X)} {ChartGeometry.Number(p.Y)}", path));
        Assert.Contains("C133.3 10 166.7 10 200 10", path);
    }
}
