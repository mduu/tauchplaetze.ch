using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Media.Queries;

/// <summary>Darf das Konto jetzt Fotos hochladen, und wie viele? Die Oberfläche prüft das, bevor sie Dateien liest.</summary>
public record GetUploadAllowanceQuery(string UploaderId);

/// <summary><see cref="Remaining"/> null: kein Limit (Moderation).</summary>
public record UploadAllowance(bool Blocked, int? Remaining)
{
    public bool Allows(int count) => !Blocked && (Remaining is null || count <= Remaining);
}

internal class GetUploadAllowanceHandler(TauchplaetzeDbContext dbContext, AccountRoles roles, TimeProvider time) : IQueryHandler<GetUploadAllowanceQuery, UploadAllowance>
{
    public async Task<UploadAllowance> Handle(GetUploadAllowanceQuery query, CancellationToken ct = default)
    {
        var rights = await roles.Of(query.UploaderId);
        if (!rights.CanWrite)
        {
            return new UploadAllowance(true, 0);
        }

        return rights.IsModerator
            ? new UploadAllowance(false, null)
            : new UploadAllowance(false, await UploadQuota.Remaining(dbContext, time.GetUtcNow(), query.UploaderId, ct));
    }
}
