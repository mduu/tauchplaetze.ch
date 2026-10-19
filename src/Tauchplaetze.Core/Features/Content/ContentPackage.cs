using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Media;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.Content;

/// <summary>
/// Format des Inhaltspakets (ZIP, `datastructure.md`, «Inhaltspaket»). Es enthält nur öffentliche Inhalte, keine Personendaten (ADR-011).
/// </summary>
internal static class ContentPackage
{
    public const int FormatVersion = 1;

    public const string ManifestEntry = "manifest.json";

    public const string WaterBodiesEntry = "water-bodies.json";

    public const string PhotosEntry = "photos.json";

    public const string SitesFolder = "dive-sites/";

    public const string PhotoFilesFolder = "photos/";

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string SiteEntry(Guid id) => $"{SitesFolder}{id}.json";

    public static string PhotoFileEntry(Guid id, string name) => $"{PhotoFilesFolder}{id}/{name}";

    public static async Task Write<T>(ZipArchive zip, string name, T value, CancellationToken ct)
    {
        await using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, ct);
    }

    public static async Task WriteBytes(ZipArchive zip, string name, byte[] content, CancellationToken ct)
    {
        await using var stream = zip.CreateEntry(name, CompressionLevel.NoCompression).Open();
        await stream.WriteAsync(content, ct);
    }

    public static async Task<T?> Read<T>(ZipArchive zip, string name, CancellationToken ct)
    {
        var entry = zip.GetEntry(name);
        if (entry is null)
        {
            return default;
        }

        await using var stream = entry.Open();
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
    }

    public static async Task<byte[]?> ReadBytes(ZipArchive zip, string name, CancellationToken ct)
    {
        var entry = zip.GetEntry(name);
        if (entry is null)
        {
            return null;
        }

        await using var stream = entry.Open();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}

/// <summary>Kopf des Pakets.</summary>
internal record PackageManifest(int FormatVersion, DateTimeOffset CreatedAt, string? Source, int WaterBodies, int DiveSites, int Photos);

/// <summary>Ein Gewässer mit den Feldern des Seeds.</summary>
internal record PackageWaterBody(
    string Slug,
    string NameDe,
    string? NameEn,
    string? NameFr,
    string? NameIt,
    WaterBodyType Type,
    PackagePoint Centroid,
    PackagePoint? SatellitePoint,
    string? AlplakesLakeKey,
    string? AlplakesModel3d,
    string? AlplakesSimstratKey,
    bool IsActive,
    IReadOnlyList<PackageHydroStation> HydroStations);

/// <summary>Eine Station des BAFU an einem Gewässer.</summary>
internal record PackageHydroStation(string Id, string Name, HydroStationRole Role, decimal? Weight);

/// <summary>Ein Punkt in WGS84.</summary>
internal record PackagePoint(double Latitude, double Longitude);

/// <summary>
/// Ein Platz: Inhalt der neuesten freigegebenen Version im Format der Versionen (`content`), das Gewässer über seinen Slug,
/// weil die Ids der Gewässer je Datenbank anders sind.
/// </summary>
internal record PackageSite(Guid Id, string WaterBody, DiveSiteStatus Status, IReadOnlyList<string> OldSlugs, JsonObject Content);

/// <summary>Ein öffentliches Foto. Wer es hochgeladen hat, fehlt bewusst. Die Namensnennung steht beim Fotografen.</summary>
internal record PackagePhoto(
    Guid Id,
    Guid DiveSiteId,
    MediaSection Section,
    Guid? EntryPointId,
    MediaKind Kind,
    int Width,
    int Height,
    DateOnly? TakenAt,
    int? Depth,
    string Photographer,
    string? PhotographerUrl,
    string? SourceUrl,
    int? SortOrder,
    IReadOnlyList<PackagePhotoText> Texts);

/// <summary>Legende und Alternativtext in einer Sprache.</summary>
internal record PackagePhotoText(string Culture, string? Caption, string? AltText);
