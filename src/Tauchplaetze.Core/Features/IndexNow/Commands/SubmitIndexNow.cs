using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Hosting;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;

namespace Tauchplaetze.Core.Features.IndexNow.Commands;

/// <summary>Meldet die seit der letzten Meldung geänderten Seiten in allen Sprachen an IndexNow (`seo.md`, «IndexNow»).</summary>
public record SubmitIndexNowCommand;

/// <param name="Urls">Gemeldete Adressen, 0 ohne Änderungen oder wenn IndexNow aus ist.</param>
/// <param name="Status">Antwort des Endpoints, null ohne Aufruf.</param>
public record SubmitIndexNowResult(int Urls, HttpStatusCode? Status);

internal class SubmitIndexNowHandler(
    IndexNowOutbox outbox,
    IndexNowClient client,
    TauchplaetzeDbContext dbContext,
    IOptions<IndexNowOptions> options,
    IOptions<PublicSiteOptions> site,
    ILogger<SubmitIndexNowHandler> logger) : ICommandHandler<SubmitIndexNowCommand, SubmitIndexNowResult>
{
    /// <summary>Höchstzahl der Adressen pro Aufruf laut IndexNow.</summary>
    private const int MaxUrlsPerRequest = 10_000;

    public async Task<SubmitIndexNowResult> Handle(SubmitIndexNowCommand command, CancellationToken ct = default)
    {
        var batch = outbox.Take();
        if (batch.IsEmpty || !options.Value.IsActive)
        {
            return new SubmitIndexNowResult(0, null);
        }

        var urls = await Urls(batch, ct);
        HttpStatusCode? status = null;
        foreach (var chunk in urls.Chunk(MaxUrlsPerRequest))
        {
            status = await Submit(chunk, ct);
            if (IsTemporary(status.Value))
            {
                outbox.Retry(batch, options.Value.MaxAttempts);
                logger.LogWarning("IndexNow answered {Status}, retrying {Urls} urls in the next interval", (int)status.Value, urls.Count);
                return new SubmitIndexNowResult(0, status);
            }

            if (!IsAccepted(status.Value))
            {
                logger.LogWarning("IndexNow rejected {Urls} urls with {Status}", chunk.Length, (int)status.Value);
            }
        }

        logger.LogInformation("IndexNow: {Urls} urls submitted, status {Status}", urls.Count, (int?)status);
        return new SubmitIndexNowResult(urls.Count, status);
    }

    private async Task<HttpStatusCode> Submit(string[] urls, CancellationToken ct)
    {
        try
        {
            return await client.Submit(new Uri(site.Value.BaseUri), urls, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "IndexNow not reachable");
            return HttpStatusCode.ServiceUnavailable;
        }
    }

    private async Task<List<string>> Urls(IndexNowBatch batch, CancellationToken ct)
    {
        var waterIds = batch.WaterBodies.Keys.ToList();
        var waterSlugs = await dbContext.WaterBodies.AsNoTracking().Where(w => waterIds.Contains(w.Id)).Select(w => w.Slug).ToListAsync(ct);
        var paths = batch.DiveSites.Keys.SelectMany(slug => PublicPaths.Languages.Select(l => PublicPaths.DiveSite(l, slug)))
            .Concat(waterSlugs.SelectMany(slug => PublicPaths.Languages.Select(l => PublicPaths.WaterBody(l, slug))));
        return paths.Select(site.Value.Link).Distinct().ToList();
    }

    private static bool IsAccepted(HttpStatusCode status) => status is HttpStatusCode.OK or HttpStatusCode.Accepted;

    private static bool IsTemporary(HttpStatusCode status) => status == HttpStatusCode.TooManyRequests || (int)status >= 500;
}
