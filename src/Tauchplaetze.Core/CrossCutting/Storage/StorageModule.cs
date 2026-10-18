using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.CrossCutting.Locks;
using Tauchplaetze.Core.CrossCutting.Modules;

namespace Tauchplaetze.Core.CrossCutting.Storage;

internal class StorageModule : ICoreModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<IRawDataStore, BlobRawDataStore>();
        services.AddSingleton<IRunLock, BlobLeaseRunLock>();
    }
}
