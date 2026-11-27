using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.CrossCutting.Modules;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Susv.Sources;

namespace Tauchplaetze.Core.Features.Susv;

internal class SusvModule : ICoreModule
{
    public void Register(IServiceCollection services)
    {
        services.AddOptions<SusvOptions>().BindConfiguration(SusvOptions.Section);
        ForecastsModule.AddSourceClient<SusvClient>(services, (_, client) => client.BaseAddress = new Uri("https://susv.ch/"));
        services.AddScoped<SusvNotifications>();
        services.AddScoped<SusvEnglish>();
    }
}
