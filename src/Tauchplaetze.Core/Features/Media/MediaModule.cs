using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.CrossCutting.Modules;

namespace Tauchplaetze.Core.Features.Media;

internal class MediaModule : ICoreModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<IMediaStore, BlobMediaStore>();
        services.AddScoped<MediaPublisher>();
    }
}
