using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.CrossCutting.Mail.Templates;
using Tauchplaetze.Core.Features.Mail.Commands;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Mail;

/// <summary>Dispatcher, Queue und Worker-Command aus Test Doubles, ohne Datenbank.</summary>
internal sealed class TestMail
{
    public FixedTime Time { get; } = new(FixedTime.SpikeDay);

    public FlakyMailSender Sender { get; } = new();

    public InMemoryMailQueue Queue { get; }

    public TestMail() => Queue = new InMemoryMailQueue(Time);

    public MailDispatcher Dispatcher => new(Queue, Sender, Templates(), Options.Create(new PublicSiteOptions { BaseUri = "https://tauchplaetze.test" }), Time, NullLogger<MailDispatcher>.Instance);

    public Task<ProcessMailQueueResult> Process() =>
        new ProcessMailQueueHandler(Queue, Sender, Dispatcher, Time, NullLogger<ProcessMailQueueHandler>.Instance)
            .Handle(new ProcessMailQueueCommand(), TestContext.Current.CancellationToken);

    public static MailTemplates Templates() => new(typeof(IMailTemplate).Assembly.GetTypes()
        .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IMailTemplate).IsAssignableFrom(t))
        .Select(t => (IMailTemplate)Activator.CreateInstance(t)!));
}
