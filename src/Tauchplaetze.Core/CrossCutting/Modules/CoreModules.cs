using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Tauchplaetze.Core.CrossCutting.Modules;

internal static class CoreModules
{
    public static IServiceCollection AddCoreModules(this IServiceCollection services, Assembly assembly)
    {
        var modules = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(ICoreModule).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .Select(t => (ICoreModule)Activator.CreateInstance(t)!);

        foreach (var module in modules)
        {
            module.Register(services);
        }

        return services;
    }
}
