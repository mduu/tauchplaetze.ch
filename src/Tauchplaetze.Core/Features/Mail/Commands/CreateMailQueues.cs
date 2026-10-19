using Tauchplaetze.Core.CrossCutting.Mail.Queue;
using Tauchplaetze.Core.CrossCutting.UseCases;

namespace Tauchplaetze.Core.Features.Mail.Commands;

/// <summary>Legt die Mail-Queues an, falls sie fehlen. Der Worker ruft das beim Start (ADR-010).</summary>
public record CreateMailQueuesCommand;

internal class CreateMailQueuesHandler(IMailQueue queue) : ICommandHandler<CreateMailQueuesCommand, Unit>
{
    public async Task<Unit> Handle(CreateMailQueuesCommand command, CancellationToken ct = default)
    {
        await queue.EnsureCreated(ct);
        return Unit.Value;
    }
}
