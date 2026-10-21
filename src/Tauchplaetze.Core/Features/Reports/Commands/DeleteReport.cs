using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Reports.Commands;

/// <summary>Löscht eine Meldung endgültig (`dive-site.md`, «Meldungen»). Die Person, die gemeldet hat, ihre eigene; ein Admin jede. Die Moderation blendet aus.</summary>
public record DeleteReportCommand(string UserId, Guid ReportId);

internal class DeleteReportHandler(TauchplaetzeDbContext dbContext, AccountRoles roles) : ICommandHandler<DeleteReportCommand, ReportResult>
{
    public async Task<ReportResult> Handle(DeleteReportCommand command, CancellationToken ct = default)
    {
        var rights = await roles.Of(command.UserId);
        var report = await dbContext.Reports.SingleOrDefaultAsync(r => r.Id == command.ReportId, ct);
        if (report is null)
        {
            return new ReportResult([ReportError.NotFound]);
        }

        if (!rights.CanWrite || (!rights.CanAdminister && report.AuthorId != command.UserId))
        {
            return new ReportResult([ReportError.NotAllowed]);
        }

        dbContext.Reports.Remove(report);
        await dbContext.SaveChangesAsync(ct);
        return new ReportResult([], report.Id);
    }
}
