namespace Tauchplaetze.Core.Features.Content;

/// <summary>
/// Puffert, was das ZIP schreibt, und gibt es mit <see cref="Drain"/> asynchron weiter. Das ZIP schliesst einen Eintrag synchron
/// (Deflate), der Body einer Antwort erlaubt das nicht. Im Speicher liegt höchstens ein Eintrag.
/// </summary>
internal sealed class EntryBuffer(Stream target) : Stream
{
    private readonly MemoryStream buffer = new();

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public async Task Drain(CancellationToken ct)
    {
        if (buffer.Length == 0)
        {
            return;
        }

        await target.WriteAsync(buffer.GetBuffer().AsMemory(0, (int)buffer.Length), ct);
        await target.FlushAsync(ct);
        buffer.SetLength(0);
    }

    public override void Write(byte[] buffer, int offset, int count) => this.buffer.Write(buffer, offset, count);

    public override void Write(ReadOnlySpan<byte> buffer) => this.buffer.Write(buffer);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        this.buffer.Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        this.buffer.Write(buffer, offset, count);
        return Task.CompletedTask;
    }

    public override void Flush()
    {
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            buffer.Dispose();
        }

        base.Dispose(disposing);
    }
}
