using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.CrossCutting.Modules;

namespace Tauchplaetze.Core.Features.Editorial;

internal class EditorialModule : ICoreModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<IRevisionStore, BlobRevisionStore>();
        services.AddScoped<RevisionWriter>();
    }
}
