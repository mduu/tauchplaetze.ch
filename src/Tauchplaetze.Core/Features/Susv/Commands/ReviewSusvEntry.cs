using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Locks;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.DiveSites;

namespace Tauchplaetze.Core.Features.Susv.Commands;

/// <summary>
/// Die Moderation prüft einen Eintrag des SUSV (ADR-013): bestätigen oder ändern, ignorieren, zurücknehmen oder eine Änderung der Quelle als gesehen markieren.
/// <see cref="SiteIds"/>, <see cref="Kind"/> und der Zeitraum zählen nur bei <see cref="SusvReviewAction.Confirm"/>. Nur Moderation und Admin.
/// </summary>
public record ReviewSusvEntryCommand(
    string ReviewerId,
    Guid EntryId,
    SusvReviewAction Action,
    IReadOnlyList<Guid>? SiteIds = null,
    NoticeKind? Kind = null,
    DateOnly? ValidFrom = null,
    DateOnly? ValidUntil = null,
    bool RecursYearly = false);

/// <summary>Ergebnis der Prüfung. Mit <see cref="Error"/> blieb der Eintrag unverändert.</summary>
public record ReviewSusvEntryResult(SusvReviewError? Error)
{
    public static readonly ReviewSusvEntryResult Ok = new((SusvReviewError?)null);
}

internal class ReviewSusvEntryHandler(TauchplaetzeDbContext dbContext, AccountRoles roles, IRunLock runLock, SusvEnglish english, TimeProvider time)
    : ICommandHandler<ReviewSusvEntryCommand, ReviewSusvEntryResult>
{
    public async Task<ReviewSusvEntryResult> Handle(ReviewSusvEntryCommand command, CancellationToken ct = default)
    {
        var rights = await roles.Of(command.ReviewerId);
        if (!rights.CanModerate)
        {
            return new ReviewSusvEntryResult(rights.IsBlocked ? SusvReviewError.Blocked : SusvReviewError.NotAllowed);
        }

        await using var acquired = await runLock.TryAcquire(ImportSusvEntriesHandler.LockName, ct);
        if (acquired is null)
        {
            return new ReviewSusvEntryResult(SusvReviewError.Busy);
        }

        var entry = await dbContext.SusvEntries.Include(e => e.Translations).Include(e => e.Sites).SingleOrDefaultAsync(e => e.Id == command.EntryId, ct);
        if (entry is null)
        {
            return new ReviewSusvEntryResult(SusvReviewError.NotFound);
        }

        var error = command.Action switch
        {
            SusvReviewAction.Confirm => await Confirm(entry, command, ct),
            SusvReviewAction.Ignore => Decide(entry, SusvReviewStatus.Ignored),
            SusvReviewAction.Reopen => Decide(entry, SusvReviewStatus.Pending),
            SusvReviewAction.MarkSeen => entry is { ReviewStatus: SusvReviewStatus.Confirmed, ChangedSinceReview: true } ? null : SusvReviewError.NotChanged,
            _ => SusvReviewError.NotAllowed,
        };
        if (error is not null)
        {
            return new ReviewSusvEntryResult(error);
        }

        MarkReviewed(entry, command.ReviewerId);
        await dbContext.SaveChangesAsync(ct);
        if (command.Action == SusvReviewAction.Confirm && entry.Topic == SusvTopic.Restriction)
        {
            await english.Update(entry.Id, ct);
        }

        return ReviewSusvEntryResult.Ok;
    }

    private async Task<SusvReviewError?> Confirm(SusvEntry entry, ReviewSusvEntryCommand command, CancellationToken ct)
    {
        var siteIds = command.SiteIds?.Distinct().ToList() ?? [];
        if (siteIds.Count == 0)
        {
            return SusvReviewError.NoSites;
        }

        var published = await dbContext.DiveSites.CountAsync(s => siteIds.Contains(s.Id) && s.Status == DiveSiteStatus.Published, ct);
        if (published != siteIds.Count)
        {
            return SusvReviewError.UnknownSite;
        }

        if (PeriodError(entry.Topic, command) is { } periodError)
        {
            return periodError;
        }

        entry.ReviewStatus = SusvReviewStatus.Confirmed;
        entry.NoticeKind = command.Kind;
        entry.ValidFrom = command.ValidFrom;
        entry.ValidUntil = command.ValidUntil;
        entry.RecursYearly = command.RecursYearly;
        entry.Sites.RemoveAll(s => !siteIds.Contains(s.DiveSiteId));
        entry.Sites.AddRange(siteIds.Where(id => entry.Sites.All(s => s.DiveSiteId != id)).Select(id => new SusvEntrySite { EntryId = entry.Id, DiveSiteId = id }));
        return null;
    }

    /// <summary>Art und Zeitraum gibt es nur bei einer Einschränkung, dort ist die Art Pflicht (`stories/002-susv-import-und-moderation.md`).</summary>
    private static SusvReviewError? PeriodError(SusvTopic topic, ReviewSusvEntryCommand command)
    {
        if (topic != SusvTopic.Restriction)
        {
            return command.Kind is not null || command.ValidFrom is not null || command.ValidUntil is not null || command.RecursYearly ? SusvReviewError.KindNotAllowed : null;
        }

        if (command.Kind is null)
        {
            return SusvReviewError.KindRequired;
        }

        if (command.RecursYearly)
        {
            return command is { ValidFrom: not null, ValidUntil: not null } ? null : SusvReviewError.YearlyNeedsDates;
        }

        return command is { ValidFrom: { } from, ValidUntil: { } until } && until < from ? SusvReviewError.InvalidPeriod : null;
    }

    /// <summary>Ignorieren und Zurücknehmen lösen die Zuordnung. Art und Zeitraum verlieren ihren Sinn.</summary>
    private static SusvReviewError? Decide(SusvEntry entry, SusvReviewStatus status)
    {
        entry.ReviewStatus = status;
        entry.Sites.Clear();
        entry.NoticeKind = null;
        entry.ValidFrom = null;
        entry.ValidUntil = null;
        entry.RecursYearly = false;
        return null;
    }

    /// <summary>Hält den geprüften Text fest. Ändert ihn die Quelle später, zeigt die Moderation alt und neu nebeneinander.</summary>
    private void MarkReviewed(SusvEntry entry, string reviewerId)
    {
        entry.ChangedSinceReview = false;
        entry.ReviewedAt = time.GetUtcNow();
        entry.ReviewedBy = reviewerId;
        foreach (var translation in entry.Translations)
        {
            translation.ReviewedTitle = translation.Title;
            translation.ReviewedText = translation.Text;
        }
    }
}
