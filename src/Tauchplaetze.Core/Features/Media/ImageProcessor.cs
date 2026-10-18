using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using SkiaSharp;

namespace Tauchplaetze.Core.Features.Media;

/// <summary>
/// Ein verarbeitetes Foto: Format des Originals, Grösse nach dem Drehen, Aufnahmedatum aus EXIF und die Bildgrössen als WebP ohne Metadaten.
/// </summary>
internal record ProcessedImage(string Extension, string ContentType, int Width, int Height, DateOnly? TakenAt, IReadOnlyList<(int Width, byte[] Content)> Renditions);

/// <summary>
/// Liest JPEG, PNG und WebP (nach dem Inhalt, nicht nach Endung oder MIME-Type), dreht nach der EXIF-Orientierung und erzeugt die Bildgrössen als WebP.
/// SkiaSharp schreibt keine Metadaten. So gelangen weder Standort (GPS) noch Kamera in die öffentlichen Dateien (ADR-009).
/// </summary>
internal static class ImageProcessor
{
    /// <summary>Höchstens so viele Pixel pro Seite. Schützt vor Dateien, die klein sind, aber riesig dekodieren.</summary>
    public const int MaxSide = 12_000;

    /// <summary>Höchstens so viele Pixel insgesamt (50 Megapixel).</summary>
    public const long MaxPixels = 50_000_000;

    private const int Quality = 82;

    /// <summary>Immer nur ein Bild gleichzeitig: Ein grosses Foto braucht beim Dekodieren viel Speicher, der Host hat wenig (App Service B1).</summary>
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static readonly Dictionary<SKEncodedImageFormat, (string Extension, string ContentType)> Formats = new()
    {
        [SKEncodedImageFormat.Jpeg] = ("jpg", "image/jpeg"),
        [SKEncodedImageFormat.Png] = ("png", "image/png"),
        [SKEncodedImageFormat.Webp] = ("webp", "image/webp"),
    };

    /// <summary><c>null</c>, wenn die Datei kein JPEG, PNG oder WebP ist, sich nicht lesen lässt oder zu gross ist.</summary>
    public static async Task<ProcessedImage?> Process(byte[] content, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            return Process(content);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static ProcessedImage? Process(byte[] content)
    {
        using var codec = SKCodec.Create(new SKManagedStream(new MemoryStream(content, writable: false), disposeManagedStream: true));
        if (codec is null || !Formats.TryGetValue(codec.EncodedFormat, out var format) || TooLarge(codec.Info))
        {
            return null;
        }

        var swap = Swaps(codec.EncodedOrigin);
        var (width, height) = swap ? (codec.Info.Height, codec.Info.Width) : (codec.Info.Width, codec.Info.Height);

        using var decoded = Decode(codec, Math.Min(width, MediaFiles.Widths[^1]), swap);
        if (decoded is null)
        {
            return null;
        }

        using var oriented = Orient(decoded, codec.EncodedOrigin, swap);
        var source = oriented ?? decoded;
        var renditions = MediaFiles.WidthsFor(width).Select(w => (w, Encode(source, w))).ToList();
        return new ProcessedImage(format.Extension, format.ContentType, width, height, TakenAt(content), renditions);
    }

    private static bool TooLarge(SKImageInfo info) =>
        info.Width <= 0 || info.Height <= 0 || info.Width > MaxSide || info.Height > MaxSide || (long)info.Width * info.Height > MaxPixels;

    private static bool Swaps(SKEncodedOrigin origin) =>
        origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

    /// <summary>
    /// Dekodiert so klein wie möglich, aber mindestens so breit wie die grösste Bildgrösse (<paramref name="neededWidth"/>, nach dem Drehen).
    /// JPEG lässt sich direkt auf 1/2, 1/4 oder 1/8 dekodieren. Das spart Speicher bei grossen Fotos.
    /// </summary>
    private static SKBitmap? Decode(SKCodec codec, int neededWidth, bool swap)
    {
        var fullWidth = swap ? codec.Info.Height : codec.Info.Width;
        var scaled = codec.GetScaledDimensions((float)neededWidth / fullWidth);
        var scaledWidth = swap ? scaled.Height : scaled.Width;
        var size = scaledWidth >= neededWidth && scaled.Width > 0 && scaled.Height > 0 ? scaled : codec.Info.Size;

        var info = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is SKCodecResult.Success or SKCodecResult.IncompleteInput)
        {
            return bitmap;
        }

        bitmap.Dispose();
        return null;
    }

    private static byte[] Encode(SKBitmap source, int width)
    {
        if (width >= source.Width)
        {
            return EncodeWebp(source);
        }

        var height = Math.Max(1, (int)Math.Round(source.Height * (double)width / source.Width));
        using var resized = source.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));
        return EncodeWebp(resized);
    }

    private static byte[] EncodeWebp(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Webp, Quality);
        return data.ToArray();
    }

    /// <summary>Gedrehte Kopie oder <c>null</c>, wenn das Bild schon richtig liegt.</summary>
    private static SKBitmap? Orient(SKBitmap source, SKEncodedOrigin origin, bool swap)
    {
        if (origin == SKEncodedOrigin.TopLeft)
        {
            return null;
        }

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
            var directories = ImageMetadataReader.ReadMetadata(new MemoryStream(content, writable: false));
            var exif = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            return exif is not null && exif.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var taken) ? DateOnly.FromDateTime(taken) : null;
        }
        catch (ImageProcessingException)
        {
            return null;
        }
    }
}
