using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using SkiaSharp;

namespace Tauchplaetze.Core.Features.Media;

/// <summary>Ein verarbeitetes Foto: Grösse nach dem Drehen, Aufnahmedatum aus EXIF und die Bildgrössen als WebP ohne Metadaten.</summary>
internal record ProcessedImage(int Width, int Height, DateOnly? TakenAt, IReadOnlyList<(int Width, byte[] Content)> Renditions);

/// <summary>
/// Liest JPEG, PNG und WebP, dreht nach der EXIF-Orientierung und erzeugt die Bildgrössen als WebP.
/// SkiaSharp schreibt keine Metadaten. So gelangen weder Standort (GPS) noch Kamera in die öffentlichen Dateien.
/// </summary>
internal static class ImageProcessor
{
    private const int Quality = 82;

    /// <summary><c>null</c>, wenn die Datei kein lesbares Bild ist.</summary>
    public static ProcessedImage? Process(byte[] content)
    {
        using var codec = SKCodec.Create(new SKMemoryStream(content));
        if (codec is null)
        {
            return null;
        }

        using var decoded = SKBitmap.Decode(codec);
        if (decoded is null)
        {
            return null;
        }

        using var oriented = Orient(decoded, codec.EncodedOrigin);
        var renditions = MediaFiles.WidthsFor(oriented.Width).Select(width => (width, Encode(oriented, width))).ToList();
        return new ProcessedImage(oriented.Width, oriented.Height, TakenAt(content), renditions);
    }

    private static byte[] Encode(SKBitmap source, int width)
    {
        var height = (int)Math.Round(source.Height * (double)width / source.Width);
        using var resized = width == source.Width ? source.Copy() : source.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));
        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(SKEncodedImageFormat.Webp, Quality);
        return data.ToArray();
    }

    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin == SKEncodedOrigin.TopLeft)
        {
            return source.Copy();
        }

        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var result = new SKBitmap(swap ? source.Height : source.Width, swap ? source.Width : source.Height);
        using var canvas = new SKCanvas(result);
        canvas.SetMatrix(OriginMatrix(origin, source.Width, source.Height));
        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
        return result;
    }

    /// <remarks>Die acht EXIF-Orientierungen als Matrix von der gespeicherten auf die angezeigte Lage.</remarks>
    private static SKMatrix OriginMatrix(SKEncodedOrigin origin, int width, int height) => origin switch
    {
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, width, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, width, 0, -1, height, 0, 0, 1),
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, height, 0, 0, 1),
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, height, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, height, -1, 0, width, 0, 0, 1),
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, width, 0, 0, 1),
        _ => SKMatrix.Identity,
    };

    private static DateOnly? TakenAt(byte[] content)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(new MemoryStream(content));
            var exif = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            return exif is not null && exif.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var taken) ? DateOnly.FromDateTime(taken) : null;
        }
        catch (ImageProcessingException)
        {
            return null;
        }
    }
}
