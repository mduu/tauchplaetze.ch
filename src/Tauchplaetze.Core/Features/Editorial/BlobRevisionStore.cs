using Azure.Storage.Blobs;

namespace Tauchplaetze.Core.Features.Editorial;

internal class BlobRevisionStore(BlobServiceClient blobs) : IRevisionStore
{
    public const string ContainerName = "revisions";

    public async Task Save(string path, byte[] content, CancellationToken ct)
    {
        var container = blobs.GetBlobContainerClient(ContainerName);
        await container.CreateIfNotExistsAsync(cancellationToken: ct);
        await container.GetBlobClient(path).UploadAsync(new BinaryData(content), overwrite: true, ct);
    }

    public async Task<byte[]> Load(string path, CancellationToken ct)
    {
        var result = await blobs.GetBlobContainerClient(ContainerName).GetBlobClient(path).DownloadContentAsync(ct);
        return result.Value.Content.ToArray();
    }
}
