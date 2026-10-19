using Azure.Storage.Blobs;

namespace Tauchplaetze.Core.CrossCutting.Storage;

internal class BlobRawDataStore(BlobServiceClient blobs) : IRawDataStore
{
    public const string ContainerName = "forecast-raw";

    public async Task Save(string path, string content, CancellationToken ct)
    {
        var container = await BlobContainers.Ensure(blobs.GetBlobContainerClient(ContainerName), ct);
        await container.GetBlobClient(path).UploadAsync(BinaryData.FromString(content), overwrite: true, ct);
    }
}
