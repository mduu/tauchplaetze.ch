using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Tauchplaetze.Core.CrossCutting.UseCases;

internal static class HandlerRegistration
{
    public static IServiceCollection AddHandlers(this IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            foreach (var contract in type.GetInterfaces().Where(IsHandlerContract))
            {
                services.AddScoped(contract, type);
            }
        }

        return services;
    }

    public static bool IsHandlerContract(Type candidate)
    {
        if (!candidate.IsGenericType)
        {
            return false;
        }

        var definition = candidate.GetGenericTypeDefinition();
        return definition == typeof(ICommandHandler<,>) || definition == typeof(IQueryHandler<,>);
    }
}
