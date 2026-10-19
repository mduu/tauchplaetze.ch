using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Content;

/// <summary>Gemeinsame Regeln von Import und Abruf.</summary>
internal static class ContentRules
{
    public static async Task<ContentError?> Refusal(AccountRoles roles, ContentOptions options, string adminId)
    {
        if (!(await roles.Of(adminId)).CanAdminister)
        {
            return ContentError.NotAllowed;
        }

        return options.ImportEnabled ? null : ContentError.Disabled;
    }

    /// <summary>
    /// Das Paket als lesbarer Stream mit Positionierung, den ZIP braucht. Ein Stream ohne Positionierung kommt in eine temporäre Datei,
    /// die beim Schliessen verschwindet. <c>null</c>, wenn das Paket grösser als <paramref name="maxBytes"/> ist.
    /// </summary>
    public static async Task<Stream?> Seekable(Stream source, long maxBytes, CancellationToken ct)
    {
        if (source.CanSeek)
        {
            return source.Length - source.Position > maxBytes ? null : new NonClosingStream(source);
        }

        var file = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                await file.DisposeAsync();
                return null;
            }

            await file.WriteAsync(buffer.AsMemory(0, read), ct);
        }

        file.Position = 0;
        return file;
    }

    /// <summary>Lässt den Stream des Aufrufers offen.</summary>
    private sealed class NonClosingStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => inner.Length;

        public override long Position { get => inner.Position; set => inner.Position = value; }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
