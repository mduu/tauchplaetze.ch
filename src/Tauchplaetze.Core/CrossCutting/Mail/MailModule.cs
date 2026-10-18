using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.CrossCutting.Modules;

namespace Tauchplaetze.Core.CrossCutting.Mail;

internal class MailModule : ICoreModule
{
    public void Register(IServiceCollection services) => services.AddSingleton<IMailSender, SmtpMailSender>();
}
