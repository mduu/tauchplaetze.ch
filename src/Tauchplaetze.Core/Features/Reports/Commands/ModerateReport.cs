using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Reports.Commands;

/// <summary>Was die Moderation mit einer Meldung tut.</summary>
public enum ReportModeration
{
    /// <summary>Kommentar öffentlich machen.</summary>
    ApproveComment,

    /// <summary>Kommentar löschen. Die Zahlen bleiben.</summary>
    RejectComment,

    /// <summary>Meldung öffentlich ausblenden.</summary>
    Hide,

    /// <summary>Ausgeblendete Meldung wieder zeigen.</summary>
    Show,
}

/// <summary>
/// Moderation einer Meldung (`dive-site.md`, «Meldungen»). Nur Moderation und Admin. <paramref name="SeenAt"/> ist <see cref="QueuedReport.UpdatedAt"/>
/// beim Laden: Eine Freigabe gilt nur für den Kommentar, den die Moderation gesehen hat.
/// </summary>
public record ModerateReportCommand(string ModeratorId, Guid ReportId, ReportModeration Action, DateTimeOffset? SeenAt = null);

internal class ModerateReportHandler(TauchplaetzeDbContext dbContext, AccountRoles roles, TimeProvider time) : ICommandHandler<ModerateReportCommand, ReportResult>
{
    public async Task<ReportResult> Handle(ModerateReportCommand command, CancellationToken ct = default)
    {
        if (!(await roles.Of(command.ModeratorId)).CanModerate)
        {
            return new ReportResult([ReportError.NotAllowed]);
        }

        var report = await dbContext.Reports.SingleOrDefaultAsync(r => r.Id == command.ReportId, ct);
        if (report is null)
        {
            return new ReportResult([ReportError.NotFound]);
        }

        if (command.Action == ReportModeration.ApproveComment && command.SeenAt != report.UpdatedAt)
        {
            return new ReportResult([ReportError.CommentChanged]);
        }

        switch (command.Action)
        {
            case ReportModeration.ApproveComment when report.Comment is not null:
                report.CommentStatus = ReportCommentStatus.Approved;
                break;
            case ReportModeration.RejectComment:
                report.Comment = null;
                report.CommentStatus = ReportCommentStatus.None;
                break;
            case ReportModeration.Hide:
                report.HiddenAt ??= time.GetUtcNow();
                report.HiddenBy ??= command.ModeratorId;
                break;
            case ReportModeration.Show:
                report.HiddenAt = null;
                report.HiddenBy = null;
                break;
        }

        await dbContext.SaveChangesAsync(ct);
        return new ReportResult([], report.Id);
    }
}
