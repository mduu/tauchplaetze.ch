using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Content.Queries;

/// <summary>Was in dieser Umgebung möglich ist (Import, Abruf, Endpoint). <c>null</c> für alle ausser Admins.</summary>
public record GetContentSyncStatusQuery(string AdminId);

internal class GetContentSyncStatusHandler(AccountRoles roles, IOptions<ContentOptions> options) : IQueryHandler<GetContentSyncStatusQuery, ContentSyncStatus?>
{
    public async Task<ContentSyncStatus?> Handle(GetContentSyncStatusQuery query, CancellationToken ct = default)
    {
        if (!(await roles.Of(query.AdminId)).CanAdminister)
        {
            return null;
        }

        var settings = options.Value;
        return new ContentSyncStatus(settings.ImportEnabled, !string.IsNullOrEmpty(settings.ExportKey), settings.SourceBaseUri?.Host);
    }
}
