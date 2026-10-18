using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace Tauchplaetze.Core.CrossCutting.Locks;

/// <remarks>Blob Lease mit 60 s, erneuert alle 20 s. Stirbt die Instanz, läuft der Lease ab und ein späterer Lauf kann starten.</remarks>
internal class BlobLeaseRunLock(BlobServiceClient blobs) : IRunLock
{
    public const string ContainerName = "locks";

    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan RenewInterval = TimeSpan.FromSeconds(20);

    public async Task<IAsyncDisposable?> TryAcquire(string name, CancellationToken ct)
    {
        var container = blobs.GetBlobContainerClient(ContainerName);
        await container.CreateIfNotExistsAsync(cancellationToken: ct);
        var blob = container.GetBlobClient(name);
        if (!await blob.ExistsAsync(ct))
        {
            await blob.UploadAsync(BinaryData.FromString(name), overwrite: false, ct);
        }

        var lease = blob.GetBlobLeaseClient();
        try
        {
            await lease.AcquireAsync(LeaseDuration, cancellationToken: ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            return null;
        }

        return new Lease(lease);
    }

    private sealed class Lease : IAsyncDisposable
    {
        private readonly BlobLeaseClient lease;

        private readonly CancellationTokenSource stop = new();

        private readonly Task renewal;

        public Lease(BlobLeaseClient lease)
        {
            this.lease = lease;
            renewal = Renew();
        }

        public async ValueTask DisposeAsync()
        {
            await stop.CancelAsync();
            await renewal;
            await lease.ReleaseAsync();
            stop.Dispose();
        }

        private async Task Renew()
        {
            using var timer = new PeriodicTimer(RenewInterval);
            try
            {
                while (await timer.WaitForNextTickAsync(stop.Token))
                {
                    await lease.RenewAsync(cancellationToken: stop.Token);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
