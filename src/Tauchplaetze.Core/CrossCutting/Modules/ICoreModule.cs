using Microsoft.Extensions.DependencyInjection;

namespace Tauchplaetze.Core.CrossCutting.Modules;

/// <summary>Was ein Slice des Core im Container anmeldet: Dienste, Clients, Optionen.</summary>
internal interface ICoreModule
{
    /// <summary>Meldet die Dienste des Slice an.</summary>
    void Register(IServiceCollection services);
}
