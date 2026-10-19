using System.Collections.Concurrent;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Tauchplaetze.Core.CrossCutting.Storage;

/// <summary>
/// Legt einen Container einmal pro Prozess an. Ein <c>CreateIfNotExists</c> bei jedem Zugriff kostet einen Request und loggt
/// beim Azure SDK jedes Mal ein 409 «ContainerAlreadyExists».
/// </summary>
internal static class BlobContainers
{
    private static readonly ConcurrentDictionary<Uri, Task> Ready = new();

    public static async Task<BlobContainerClient> Ensure(BlobContainerClient container, CancellationToken ct, PublicAccessType access = PublicAccessType.None)
    {
        var creation = Ready.GetOrAdd(container.Uri, _ => container.CreateIfNotExistsAsync(access, cancellationToken: CancellationToken.None));
        try
        {
            await creation.WaitAsync(ct);
        }
        catch (Exception) when (creation.IsFaulted)
        {
            Ready.TryRemove(container.Uri, out _);
            throw;
        }

        return container;
    }
}
