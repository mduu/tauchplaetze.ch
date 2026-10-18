using System.Buffers.Binary;
using System.Text;
using SkiaSharp;

namespace Tauchplaetze.Core.Tests.Features.Media;

/// <summary>Kleine Testbilder. Ein JPEG trägt auf Wunsch EXIF mit Aufnahmedatum und GPS, wie ein Foto vom Handy.</summary>
internal static class TestImages
{
    public static byte[] Jpeg(int width = 1200, int height = 800, DateTime? takenAt = null)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(20, 90, 110));
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        var jpeg = data.ToArray();
        return takenAt is { } date ? WithExif(jpeg, date) : jpeg;
    }

    /// <summary>Fügt nach dem SOI ein APP1-Segment mit IFD0, Exif-IFD (DateTimeOriginal) und GPS-IFD (Breitengrad) ein.</summary>
    private static byte[] WithExif(byte[] jpeg, DateTime takenAt)
    {
        var tiff = new byte[130];
        Encoding.ASCII.GetBytes("II").CopyTo(tiff, 0);
        U16(tiff, 2, 42);
        U32(tiff, 4, 8);

        U16(tiff, 8, 2);
        Entry(tiff, 10, 0x8769, 4, 1, 38);
        Entry(tiff, 22, 0x8825, 4, 1, 76);

        U16(tiff, 38, 1);
        Entry(tiff, 40, 0x9003, 2, 20, 56);
        Encoding.ASCII.GetBytes(takenAt.ToString("yyyy:MM:dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + "\0").CopyTo(tiff, 56);

        U16(tiff, 76, 2);
        Entry(tiff, 78, 0x0001, 2, 2, BinaryPrimitives.ReadUInt32LittleEndian("N\0\0\0"u8));
        Entry(tiff, 90, 0x0002, 5, 3, 106);
        uint[] latitude = [47, 1, 17, 1, 5, 1];
        for (var i = 0; i < latitude.Length; i++)
        {
            U32(tiff, 106 + (i * 4), latitude[i]);
        }

        var header = Encoding.ASCII.GetBytes("Exif\0\0");
        var length = 2 + header.Length + tiff.Length;
        byte[] segment = [0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. header, .. tiff];
        return [.. jpeg[..2], .. segment, .. jpeg[2..]];
    }

    private static void Entry(byte[] tiff, int at, ushort tag, ushort type, uint count, uint value)
    {
        U16(tiff, at, tag);
        U16(tiff, at + 2, type);
        U32(tiff, at + 4, count);
        U32(tiff, at + 8, value);
    }

    private static void U16(byte[] buffer, int at, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(at), value);

    private static void U32(byte[] buffer, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(at), value);
}
