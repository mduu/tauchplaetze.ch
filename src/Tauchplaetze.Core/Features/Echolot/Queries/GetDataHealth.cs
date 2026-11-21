using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;

namespace Tauchplaetze.Core.Features.Echolot.Queries;

/// <summary>Der Gesamtzustand der Datenquellen für den Eintrag «Echolot» im Info-Menü (`ux.md`, «Echolot»).</summary>
public record GetDataHealthQuery;

internal class GetDataHealthHandler(TauchplaetzeDbContext dbContext, TimeProvider time) : IQueryHandler<GetDataHealthQuery, DataHealth>
{
    public async Task<DataHealth> Handle(GetDataHealthQuery query, CancellationToken ct = default) =>
        EcholotRules.Overall(await DataSourceStatuses.Read(dbContext, time, ct));
}
