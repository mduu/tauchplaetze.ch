using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.CrossCutting.Modules;

namespace Tauchplaetze.Core.Features.IndexNow;

internal class IndexNowModule : ICoreModule
{
    private const string UserAgent = "tauchplaetze.ch/1.0 (+https://tauchplaetze.ch)";

    public void Register(IServiceCollection services)
    {
        services.AddSingleton<IndexNowOutbox>();
        services.AddHttpClient<IndexNowClient>(client => client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent));
    }
}
