using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Translations.Queries;

/// <summary>Ob das Konto jetzt automatisch übersetzen darf. Der Editor zeigt den Button nur dann.</summary>
public record GetTranslationAllowanceQuery(string UserId);

internal class GetTranslationAllowanceHandler(AccountRoles roles, TranslationQuota quota) : IQueryHandler<GetTranslationAllowanceQuery, TranslationAllowance>
{
    public async Task<TranslationAllowance> Handle(GetTranslationAllowanceQuery query, CancellationToken ct = default)
    {
        var rights = await roles.Of(query.UserId);
        var error = await quota.Check(query.UserId, rights, ct);
        return new TranslationAllowance(error, error is TranslationError.NotAllowed or TranslationError.Unavailable ? 0 : await quota.RemainingToday(query.UserId, rights, ct));
    }
}
