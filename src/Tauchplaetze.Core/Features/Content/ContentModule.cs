using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.CrossCutting.Modules;

namespace Tauchplaetze.Core.Features.Content;

internal class ContentModule : ICoreModule
{
    /// <remarks>
    /// Ohne <c>RemoveAllResilienceHandlers</c> bricht der Handler der Service Defaults den Download nach 30 s ab (wie bei den Quellen der Prognose).
    /// Ein Paket mit Fotos braucht länger. Kein Retry: Der Abruf startet von Hand.
    /// </remarks>
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers ist experimentell, aber der einzige Weg, den Handler der Service Defaults pro Client zu ersetzen.
    public void Register(IServiceCollection services)
    {
        services.AddScoped<ContentImporter>();
        services.AddHttpClient<ContentSourceClient>(client => client.Timeout = TimeSpan.FromMinutes(15))
            .ConfigurePrimaryHttpMessageHandler(ContentSourceClient.Handler)
            .RedactLoggedHeaders([ContentOptions.KeyHeader])
            .RemoveAllResilienceHandlers();
    }
#pragma warning restore EXTEXP0001
}
