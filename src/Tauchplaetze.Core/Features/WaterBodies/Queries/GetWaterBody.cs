using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;

namespace Tauchplaetze.Core.Features.WaterBodies.Queries;

/// <summary>Ein aktives Gewässer über seinen Slug, mit dem Namen in der Sprache <paramref name="Culture"/>. <c>null</c>, wenn es keines gibt.</summary>
public record GetWaterBodyQuery(string Slug, string Culture);

/// <summary>Ein Gewässer mit Mittelpunkt für die Karte.</summary>
public record WaterBodyDetails(int Id, string Slug, string Name, WaterBodyType Type, double Latitude, double Longitude);

internal class GetWaterBodyHandler(TauchplaetzeDbContext dbContext) : IQueryHandler<GetWaterBodyQuery, WaterBodyDetails?>
{
    public async Task<WaterBodyDetails?> Handle(GetWaterBodyQuery query, CancellationToken ct = default)
    {
        var water = await dbContext.WaterBodies
            .AsNoTracking()
            .Where(w => w.Slug == query.Slug && w.IsActive)
            .Select(w => new { w.Id, w.Slug, w.NameDe, w.NameEn, w.NameFr, w.NameIt, w.WaterBodyType, w.Centroid })
            .SingleOrDefaultAsync(ct);

        return water is null
            ? null
            : new WaterBodyDetails(
                water.Id,
                water.Slug,
                WaterBodyNames.In(query.Culture, water.NameDe, water.NameEn, water.NameFr, water.NameIt),
                water.WaterBodyType,
                water.Centroid.Y,
                water.Centroid.X);
    }
}
