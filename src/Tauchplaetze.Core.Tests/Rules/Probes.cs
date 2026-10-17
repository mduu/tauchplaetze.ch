using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;

namespace Tauchplaetze.Core.Tests.Rules.Probes;

/// <summary>Gegenbeispiel: öffentlich und nicht in `Queries`.</summary>
public class PublicHandlerInWrongPlace : IQueryHandler<string, int>
{
    public Task<int> Handle(string query, CancellationToken ct = default) => Task.FromResult(0);
}

/// <summary>Gegenbeispiel: Query-Handler, der im `async`-Body speichert.</summary>
internal class WritingQueryHandler(TauchplaetzeDbContext dbContext) : IQueryHandler<int, int>
{
    public async Task<int> Handle(int query, CancellationToken ct = default) =>
        await dbContext.SaveChangesAsync(ct);
}
