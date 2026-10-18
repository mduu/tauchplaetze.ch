using Azure.Storage.Blobs;

namespace Tauchplaetze.Core.CrossCutting.Storage;

internal class BlobRawDataStore(BlobServiceClient blobs) : IRawDataStore
{
    public const string ContainerName = "forecast-raw";

    public async Task Save(string path, string content, CancellationToken ct)
    {
        var container = blobs.GetBlobContainerClient(ContainerName);
        await container.CreateIfNotExistsAsync(cancellationToken: ct);
        await container.GetBlobClient(path).UploadAsync(BinaryData.FromString(content), overwrite: true, ct);
    }
}
