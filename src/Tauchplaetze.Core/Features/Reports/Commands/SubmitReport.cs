using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;

namespace Tauchplaetze.Core.Features.Reports.Commands;

/// <summary>
/// Meldet einen Tauchgang (`dive-site.md`, «Meldungen»). Eine zweite Meldung für denselben Platz und Tag ersetzt die eigene.
/// Ein neuer oder geänderter Kommentar wartet auf die Moderation.
/// </summary>
public record SubmitReportCommand(string UserId, Guid DiveSiteId, DateOnly Date, decimal Visibility, decimal? Temperature, int? TemperatureDepth, string? Comment);

internal class SubmitReportHandler(TauchplaetzeDbContext dbContext, UserManager<User> users, TimeProvider time) : ICommandHandler<SubmitReportCommand, ReportResult>
{
    public async Task<ReportResult> Handle(SubmitReportCommand command, CancellationToken ct = default)
    {
        if (await users.FindByIdAsync(command.UserId) is not { EmailConfirmed: true, BlockedAt: null })
        {
            return new ReportResult([ReportError.NotAllowed]);
        }

        var comment = ReportComment.Normalize(command.Comment);
        var errors = Validate(command, comment, SwissTime.Today(time));
        if (!await dbContext.DiveSites.AnyAsync(d => d.Id == command.DiveSiteId && d.Status == DiveSiteStatus.Published, ct))
        {
            errors.Insert(0, ReportError.SiteNotFound);
        }

        if (errors.Count > 0)
        {
            return new ReportResult(errors);
        }

        var now = time.GetUtcNow();
        var report = await dbContext.Reports.SingleOrDefaultAsync(r => r.DiveSiteId == command.DiveSiteId && r.AuthorId == command.UserId && r.Date == command.Date, ct);
        if (report is null && await SubmittedToday(command.UserId, now, ct) >= ReportRules.MaxPerDay)
        {
            return new ReportResult([ReportError.DailyLimit]);
        }

        if (report is null)
        {
            report = new Report { Id = Guid.CreateVersion7(), DiveSiteId = command.DiveSiteId, AuthorId = command.UserId, Date = command.Date, CreatedAt = now };
            dbContext.Reports.Add(report);
        }

        if (comment != report.Comment)
        {
            report.CommentStatus = comment is null ? ReportCommentStatus.None : ReportCommentStatus.Pending;
        }

        var forecast = await dbContext.DailyForecasts.AsNoTracking()
            .Where(f => f.DiveSiteId == command.DiveSiteId && f.Date == command.Date)
            .Select(f => new { f.DiveIndex, f.ModelVersion })
            .SingleOrDefaultAsync(ct);

        report.Visibility = command.Visibility;
        report.Temperature = command.Temperature;
        report.TemperatureDepth = command.Temperature is null ? null : command.TemperatureDepth;
        report.Comment = comment;
        report.UpdatedAt = now;
        report.ForecastIndex = forecast?.DiveIndex;
        report.ForecastModelVersion = forecast?.ModelVersion;
        await dbContext.SaveChangesAsync(ct);
        return new ReportResult([], report.Id);
    }

    private static List<ReportError> Validate(SubmitReportCommand command, string? comment, DateOnly today)
    {
        var errors = new List<ReportError>();
        if (command.Date > today || command.Date < today.AddDays(-ReportRules.MaxDaysBack))
        {
            errors.Add(ReportError.InvalidDate);
        }

        if (command.Visibility is < ReportRules.MinVisibility or > ReportRules.MaxVisibility || command.Visibility * 2 % 1 != 0)
        {
            errors.Add(ReportError.InvalidVisibility);
        }

        if (command.Temperature is < ReportRules.MinTemperature or > ReportRules.MaxTemperature || command.Temperature * 10 % 1 is not (0 or null))
        {
            errors.Add(ReportError.InvalidTemperature);
        }

        if (command.TemperatureDepth is not null && (command.Temperature is null || command.TemperatureDepth is < 0 or > ReportRules.MaxDepth))
        {
            errors.Add(ReportError.InvalidDepth);
        }

        if (comment?.Length > ReportRules.CommentMaxLength)
        {
            errors.Add(ReportError.CommentTooLong);
        }

        return errors;
    }

    private async Task<int> SubmittedToday(string userId, DateTimeOffset now, CancellationToken ct)
    {
        var today = SwissTime.DayOf(now);
        var from = SwissTime.ToUtc(today, 0);
        var to = SwissTime.ToUtc(today.AddDays(1), 0);
        return await dbContext.Reports.CountAsync(r => r.AuthorId == userId && r.CreatedAt >= from && r.CreatedAt < to, ct);
    }
}
