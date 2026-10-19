using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace Tauchplaetze.Core.Features.Media;

/// <remarks>Die Container entstehen einmal pro Prozess. In Prod legt das Deployment `media` mit öffentlichem Lesezugriff an (`infrastructure.md`).</remarks>
internal class BlobMediaStore(BlobServiceClient blobs, IOptions<MediaOptions> options) : IMediaStore
{
    private readonly SemaphoreSlim containerGate = new(1, 1);

    private bool containersReady;

    public const string PendingContainer = "media-pending";

    public const string PublicContainer = "media";

    private const string CacheControl = "public, max-age=31536000, immutable";

    public async Task SavePending(Guid mediaId, string name, byte[] content, string contentType, CancellationToken ct)
    {
        await EnsureContainers(ct);
        await blobs.GetBlobContainerClient(PendingContainer).GetBlobClient($"{mediaId}/{name}").UploadAsync(
            new BinaryData(content),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            ct);
    }

    public async Task<byte[]?> LoadPending(Guid mediaId, string name, CancellationToken ct)
    {
        var blob = blobs.GetBlobContainerClient(PendingContainer).GetBlobClient($"{mediaId}/{name}");
        if (!await blob.ExistsAsync(ct))
        {
            return null;
        }

        return (await blob.DownloadContentAsync(ct)).Value.Content.ToArray();
    }

    public async Task Publish(Guid mediaId, IReadOnlyList<string> names, CancellationToken ct)
    {
        foreach (var name in names)
        {
            var content = await LoadPending(mediaId, name, ct) ?? throw new InvalidOperationException($"Datei {mediaId}/{name} fehlt.");
            await SavePublic(mediaId, name, content, ct);
        }
    }

    public async Task<byte[]?> LoadPublic(Guid mediaId, string name, CancellationToken ct)
    {
        var blob = blobs.GetBlobContainerClient(PublicContainer).GetBlobClient($"{mediaId}/{name}");
        if (!await blob.ExistsAsync(ct))
        {
            return null;
        }

        return (await blob.DownloadContentAsync(ct)).Value.Content.ToArray();
    }

    public async Task SavePublic(Guid mediaId, string name, byte[] content, CancellationToken ct)
    {
        await EnsureContainers(ct);
        await blobs.GetBlobContainerClient(PublicContainer).GetBlobClient($"{mediaId}/{name}").UploadAsync(
            new BinaryData(content),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = MediaFiles.ContentTypeOf(name), CacheControl = CacheControl } },
            ct);
    }

    public Task DeletePending(Guid mediaId, CancellationToken ct) => DeleteAll(PendingContainer, mediaId, ct);

    public Task DeletePublic(Guid mediaId, CancellationToken ct) => DeleteAll(PublicContainer, mediaId, ct);

    private async Task DeleteAll(string containerName, Guid mediaId, CancellationToken ct)
    {
        await EnsureContainers(ct);
        var container = blobs.GetBlobContainerClient(containerName);
        await foreach (var blob in container.GetBlobsAsync(new GetBlobsOptions { Prefix = $"{mediaId}/" }, ct))
        {
            await container.DeleteBlobIfExistsAsync(blob.Name, cancellationToken: ct);
        }
    }

    private async Task EnsureContainers(CancellationToken ct)
    {
        if (containersReady)
        {
            return;
        }

        await containerGate.WaitAsync(ct);
        try
        {
            if (!containersReady)
            {
                await blobs.GetBlobContainerClient(PendingContainer).CreateIfNotExistsAsync(cancellationToken: ct);
                await blobs.GetBlobContainerClient(PublicContainer).CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);
                containersReady = true;
            }
        }
        finally
        {
            containerGate.Release();
        }
    }

    public Uri PublicUri(Guid mediaId, string name) =>
        new($"{(options.Value.PublicBaseUri ?? blobs.GetBlobContainerClient(PublicContainer).Uri).ToString().TrimEnd('/')}/{mediaId}/{name}");
}
