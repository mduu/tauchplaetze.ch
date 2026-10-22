using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Translations;

/// <summary>Tageslimit pro Konto nach Rolle und Budget pro Monat für alle (`cms.md`, «Automatisch übersetzen»). Nur bezahlte Übersetzungen zählen, Treffer im Cache nicht.</summary>
internal class TranslationQuota(TauchplaetzeDbContext dbContext, IOptions<TranslationOptions> options, TimeProvider time)
{
    public async Task<TranslationError?> Check(string userId, AccountRights rights, CancellationToken ct)
    {
        if (!rights.CanWrite)
        {
            return TranslationError.NotAllowed;
        }

        if (!options.Value.IsConfigured)
        {
            return TranslationError.Unavailable;
        }

        if (await RemainingToday(userId, rights, ct) <= 0)
        {
            return TranslationError.DailyLimit;
        }

        return await SpentThisMonth(ct) >= options.Value.MonthlyBudgetUsd ? TranslationError.BudgetExhausted : null;
    }

    /// <summary><c>null</c>: kein Tageslimit (Admin).</summary>
    public async Task<int?> RemainingToday(string userId, AccountRights rights, CancellationToken ct)
    {
        if (rights.IsAdmin)
        {
            return null;
        }

        var limit = rights.IsModerator ? options.Value.DailyLimitModerator : options.Value.DailyLimitContributor;
        var today = SwissTime.DayOf(time.GetUtcNow());
        var from = SwissTime.ToUtc(today, 0);
        var to = SwissTime.ToUtc(today.AddDays(1), 0);
        var used = await dbContext.MachineTranslations.CountAsync(t => t.RequestedBy == userId && t.CreatedAt >= from && t.CreatedAt < to, ct);
        return Math.Max(0, limit - used);
    }

    private async Task<decimal> SpentThisMonth(CancellationToken ct)
    {
        var today = SwissTime.DayOf(time.GetUtcNow());
        var from = SwissTime.ToUtc(new DateOnly(today.Year, today.Month, 1), 0);
        var tokens = await dbContext.MachineTranslations.Where(t => t.CreatedAt >= from)
            .GroupBy(_ => 1)
            .Select(g => new { Input = g.Sum(t => (long)t.InputTokens), Output = g.Sum(t => (long)t.OutputTokens) })
            .SingleOrDefaultAsync(ct);
        return tokens is null ? 0 : (tokens.Input * options.Value.InputPricePerMillion + tokens.Output * options.Value.OutputPricePerMillion) / 1_000_000m;
    }
}
