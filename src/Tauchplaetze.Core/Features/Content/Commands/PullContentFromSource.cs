using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Content.Commands;

/// <summary>
/// Holt das Inhaltspaket von Prod (`Content:SourceBaseUri`, nur HTTPS) und übernimmt es wie <see cref="ImportContentCommand"/> (ADR-011).
/// </summary>
public record PullContentFromSourceCommand(string AdminId);

internal class PullContentFromSourceHandler(
    ContentImporter importer,
    ContentSourceClient source,
    AccountRoles roles,
    IOptions<ContentOptions> options,
    ILogger<PullContentFromSourceHandler> logger) : ICommandHandler<PullContentFromSourceCommand, ImportContentResult>
{
    public async Task<ImportContentResult> Handle(PullContentFromSourceCommand command, CancellationToken ct = default)
    {
        var settings = options.Value;
        if (await ContentRules.Refusal(roles, settings, command.AdminId) is { } error)
        {
            return ImportContentResult.Fail(error);
        }

        if (ContentRules.Source(settings) is not { } baseUri || string.IsNullOrEmpty(settings.ExportKey))
        {
            return ImportContentResult.Fail(ContentError.SourceNotConfigured);
        }

        Stream? package;
        try
        {
            package = await source.Download(baseUri, settings.ExportKey, settings.MaxPackageBytes, ct);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning("Content pull from {Host} failed: {Error}", baseUri.Host, exception.GetType().Name);
            package = null;
        }

        if (package is null)
        {
            return ImportContentResult.Fail(ContentError.SourceFailed);
        }

        await using (package)
        {
            return await importer.Import(package, command.AdminId, ct);
        }
    }
}
