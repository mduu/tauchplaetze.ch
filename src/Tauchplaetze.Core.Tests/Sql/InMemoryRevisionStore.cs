using System.Collections.Concurrent;
using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Tests.Sql;

internal sealed class InMemoryRevisionStore : IRevisionStore
{
    private readonly ConcurrentDictionary<string, byte[]> blobs = new();

    public IReadOnlyCollection<string> Paths => [.. blobs.Keys];

    public Task Save(string path, byte[] content, CancellationToken ct)
    {
        blobs[path] = content;
        return Task.CompletedTask;
    }

    public Task<byte[]> Load(string path, CancellationToken ct) => Task.FromResult(blobs[path]);

    public void Clear() => blobs.Clear();
}
