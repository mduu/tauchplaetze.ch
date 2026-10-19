using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.CrossCutting.Mail.Queue;
using Tauchplaetze.Core.CrossCutting.Mail.Templates;
using Tauchplaetze.Core.CrossCutting.Modules;

namespace Tauchplaetze.Core.CrossCutting.Mail;

internal class MailModule : ICoreModule
{
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<IMailSender, SmtpMailSender>();
        services.AddSingleton<IMailQueue, StorageMailQueue>();
        services.AddSingleton<MailTemplates>();
        foreach (var template in typeof(MailModule).Assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IMailTemplate).IsAssignableFrom(t)))
        {
            services.AddSingleton(typeof(IMailTemplate), template);
        }
        services.AddScoped<MailDispatcher>();
    }
}
