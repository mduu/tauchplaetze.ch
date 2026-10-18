using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace Tauchplaetze.Core.Features.Media;

internal class BlobMediaStore(BlobServiceClient blobs, IOptions<MediaOptions> options) : IMediaStore
{
    public const string PendingContainer = "media-pending";

    public const string PublicContainer = "media";

    private const string CacheControl = "public, max-age=31536000, immutable";

    public async Task SavePending(Guid mediaId, string name, byte[] content, string contentType, CancellationToken ct)
    {
        var container = blobs.GetBlobContainerClient(PendingContainer);
        await container.CreateIfNotExistsAsync(cancellationToken: ct);
        await container.GetBlobClient($"{mediaId}/{name}").UploadAsync(
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
        var target = blobs.GetBlobContainerClient(PublicContainer);
        await target.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);
        foreach (var name in names)
        {
            var content = await LoadPending(mediaId, name, ct) ?? throw new InvalidOperationException($"Datei {mediaId}/{name} fehlt.");
            await target.GetBlobClient($"{mediaId}/{name}").UploadAsync(
                new BinaryData(content),
                new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = MediaFiles.ContentTypeOf(name), CacheControl = CacheControl } },
                ct);
        }
    }

    public async Task DeletePending(Guid mediaId, CancellationToken ct)
    {
        var container = blobs.GetBlobContainerClient(PendingContainer);
        if (!await container.ExistsAsync(ct))
        {
            return;
        }

        await foreach (var blob in container.GetBlobsAsync(new GetBlobsOptions { Prefix = $"{mediaId}/" }, ct))
        {
            await container.DeleteBlobIfExistsAsync(blob.Name, cancellationToken: ct);
        }
    }

    public Uri PublicUri(Guid mediaId, string name) =>
        new($"{(options.Value.PublicBaseUri ?? blobs.GetBlobContainerClient(PublicContainer).Uri).ToString().TrimEnd('/')}/{mediaId}/{name}");
}
