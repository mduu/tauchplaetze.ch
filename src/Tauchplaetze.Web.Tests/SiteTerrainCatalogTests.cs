using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Tauchplaetze.Web.Features.DiveSites;

namespace Tauchplaetze.Web.Tests;

public sealed class SiteTerrainCatalogTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("terrain-test").FullName;

    [Theory]
    [InlineData(46.6879, 7.7441, "terrain/1_1.bin")]
    [InlineData(46.68805, 7.7441, "terrain/2_2.bin")]
    [InlineData(46.6879, 7.7450, null)]
    public void Ein_Platz_bekommt_das_naechste_Raster_hoechstens_60_m_entfernt(double latitude, double longitude, string? expected)
    {
        // Arrange
        Write("""{"points":121,"cellSize":2,"sites":[{"lat":46.6879,"lng":7.7441,"e":1,"n":1,"file":"1_1.bin"},{"lat":46.6881,"lng":7.7441,"e":2,"n":2,"file":"2_2.bin"}]}""", "1_1.bin", "2_2.bin");
        using var files = new PhysicalFileProvider(root);
        var catalog = new SiteTerrainCatalog(files, NullLogger<SiteTerrainCatalog>.Instance);

        // Act
        var terrain = catalog.Find(latitude, longitude);

        // Assert
        Assert.Equal(expected, terrain?.File);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("kein json")]
    [InlineData("""{"points":121,"cellSize":2,"sites":[null]}""")]
    [InlineData("""{"points":121,"cellSize":2,"sites":[{"lat":46.6879,"lng":7.7441,"e":1,"n":1}]}""")]
    [InlineData("""{"points":121,"cellSize":2,"sites":[{"lat":46.6879,"lng":7.7441,"e":1,"n":1,"file":"fehlt.bin"}]}""")]
    public void Ein_kaputter_Index_oder_Eintrag_schaltet_die_3D_Ansicht_aus(string json)
    {
        // Arrange
        Write(json);
        using var files = new PhysicalFileProvider(root);
        var catalog = new SiteTerrainCatalog(files, NullLogger<SiteTerrainCatalog>.Instance);

        // Act
        var terrain = catalog.Find(46.6879, 7.7441);

        // Assert
        Assert.Null(terrain);
    }

    public void Dispose() => Directory.Delete(root, true);

    private void Write(string json, params string[] rasters)
    {
        Directory.CreateDirectory(Path.Combine(root, "terrain"));
        File.WriteAllText(Path.Combine(root, "terrain", "index.json"), json);
        foreach (var raster in rasters)
        {
            File.WriteAllBytes(Path.Combine(root, "terrain", raster), []);
        }
    }
}
