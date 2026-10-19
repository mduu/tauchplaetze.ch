using System.Collections.Concurrent;
using Tauchplaetze.Core.Features.Media;

namespace Tauchplaetze.Core.Tests.Sql;

internal sealed class InMemoryMediaStore : IMediaStore
{
    public ConcurrentDictionary<string, byte[]> Pending { get; } = new();

    public ConcurrentDictionary<string, byte[]> Public { get; } = new();

    public Task SavePending(Guid mediaId, string name, byte[] content, string contentType, CancellationToken ct)
    {
        Pending[$"{mediaId}/{name}"] = content;
        return Task.CompletedTask;
    }

    public Task<byte[]?> LoadPending(Guid mediaId, string name, CancellationToken ct) =>
        Task.FromResult(Pending.GetValueOrDefault($"{mediaId}/{name}"));

    public Task Publish(Guid mediaId, IReadOnlyList<string> names, CancellationToken ct)
    {
        foreach (var name in names)
        {
            Public[$"{mediaId}/{name}"] = Pending[$"{mediaId}/{name}"];
        }

        return Task.CompletedTask;
    }

    public Task<byte[]?> LoadPublic(Guid mediaId, string name, CancellationToken ct) =>
        Task.FromResult(Public.GetValueOrDefault($"{mediaId}/{name}"));

    public Task SavePublic(Guid mediaId, string name, byte[] content, CancellationToken ct)
    {
        Public[$"{mediaId}/{name}"] = content;
        return Task.CompletedTask;
    }

    public Task DeletePending(Guid mediaId, CancellationToken ct)
    {
        foreach (var key in Pending.Keys.Where(k => k.StartsWith($"{mediaId}/", StringComparison.Ordinal)))
        {
            Pending.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task DeletePublic(Guid mediaId, CancellationToken ct)
    {
        foreach (var key in Public.Keys.Where(k => k.StartsWith($"{mediaId}/", StringComparison.Ordinal)))
        {
            Public.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Uri PublicUri(Guid mediaId, string name) => new($"https://media.tauchplaetze.test/{mediaId}/{name}");

    public void Clear()
    {
        Pending.Clear();
        Public.Clear();
    }
}
