using Microsoft.AspNetCore.OutputCaching;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Contact.Commands;
using Tauchplaetze.Core.Features.Forecasts.Commands;
using Tauchplaetze.Core.Features.Mail.Commands;
using Tauchplaetze.Core.Features.Media.Commands;
using Tauchplaetze.Core.Features.Operations.Commands;
using Tauchplaetze.Core.Features.Translations.Commands;

namespace Tauchplaetze.Web.CrossCutting.Caching;

/// <summary>
/// Leert den Output Cache nach jedem Command, der öffentliche Seiten ändern kann. Standard: alle Seiten.
/// Ein vergessener Command leert so zu viel, nie zu wenig. Ausgenommen sind nur Commands, die keine öffentliche Seite ändern.
/// </summary>
internal static class CommandCacheEviction
{
    private static readonly HashSet<Type> WithoutPublicChange =
    [
        typeof(SignInCommand),
        typeof(RegisterCommand),
        typeof(ConfirmEmailCommand),
        typeof(RequestPasswordResetCommand),
        typeof(SendPasswordResetCommand),
        typeof(ResetPasswordCommand),
        typeof(SendContactMessageCommand),
        typeof(StartForecastRunCommand),
        typeof(ProcessMailQueueCommand),
        typeof(CheckDeadLettersCommand),
        typeof(DeleteDeadLettersCommand),
        typeof(ReplayDeadLettersCommand),
        typeof(CreateMailQueuesCommand),
        typeof(UploadMediaCommand),
        typeof(PurgeUnusedMediaCommand),
        typeof(MigrateDatabaseCommand),
        typeof(TranslateSiteTextCommand),
    ];

    /// <summary>Der Tag, den ein Command aus dem Cache nimmt. Null: keiner.</summary>
    public static string? TagFor(Type command) => command switch
    {
        _ when command == typeof(ImportDischargeCommand) => PublicPageCache.DischargePages,
        _ when WithoutPublicChange.Contains(command) => null,
        _ => PublicPageCache.AllPages,
    };

    /// <summary>
    /// Umhüllt jeden angemeldeten Command Handler. Nach dem Anmelden des Core aufrufen.
    /// Bereits umhüllte Handler (Factory) bleiben unverändert.
    /// </summary>
    public static IServiceCollection EvictPublicPagesAfterCommands(this IServiceCollection services)
    {
        for (var i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];
            if (IsCommandHandler(descriptor.ServiceType) && descriptor.ImplementationFactory is null && !descriptor.IsKeyedService)
            {
                services[i] = Wrap(descriptor);
            }
        }

        return services;
    }

    private static bool IsCommandHandler(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICommandHandler<,>);

    private static ServiceDescriptor Wrap(ServiceDescriptor descriptor)
    {
        var arguments = descriptor.ServiceType.GetGenericArguments();
        var wrapper = typeof(EvictingCommandHandler<,>).MakeGenericType(arguments);
        return new ServiceDescriptor(descriptor.ServiceType, provider =>
        {
            var inner = descriptor.ImplementationInstance
                ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
            return ActivatorUtilities.CreateInstance(provider, wrapper, inner, TagFor(arguments[0]) ?? "");
        }, descriptor.Lifetime);
    }

    private sealed class EvictingCommandHandler<TCommand, TResult>(ICommandHandler<TCommand, TResult> inner, string tag, IOutputCacheStore store)
        : ICommandHandler<TCommand, TResult>
    {
        public async Task<TResult> Handle(TCommand command, CancellationToken ct = default)
        {
            var result = await inner.Handle(command, ct);
            if (tag.Length > 0)
            {
                await store.EvictByTagAsync(tag, CancellationToken.None);
            }

            return result;
        }
    }
}
