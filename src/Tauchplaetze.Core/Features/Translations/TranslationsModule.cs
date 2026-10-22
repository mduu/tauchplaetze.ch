using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.CrossCutting.Modules;

namespace Tauchplaetze.Core.Features.Translations;

internal class TranslationsModule : ICoreModule
{
    public void Register(IServiceCollection services)
    {
        services.AddScoped<TranslationQuota>();
        services.AddSingleton<ITextTranslator, ClaudeTextTranslator>();
    }
}
