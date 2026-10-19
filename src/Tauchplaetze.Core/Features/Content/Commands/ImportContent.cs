using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Features.Content.Commands;

/// <summary>
/// Übernimmt ein Inhaltspaket (ADR-011). Nur für Admins und nur, wenn `Content:ImportEnabled` gesetzt ist (lokal, nie in Prod).
/// Legt an, was fehlt, hängt eine freigegebene Version an, wo der Inhalt anders ist, und löscht nie.
/// </summary>
public record ImportContentCommand(string AdminId, Stream Package);

internal class ImportContentHandler(ContentImporter importer, AccountRoles roles, IOptions<ContentOptions> options)
    : ICommandHandler<ImportContentCommand, ImportContentResult>
{
    public async Task<ImportContentResult> Handle(ImportContentCommand command, CancellationToken ct = default)
    {
        if (await ContentRules.Refusal(roles, options.Value, command.AdminId) is { } error)
        {
            return ImportContentResult.Fail(error);
        }

        await using var package = await ContentRules.Seekable(command.Package, options.Value.MaxPackageBytes, ct);
        return package is null ? ImportContentResult.Fail(ContentError.InvalidPackage) : await importer.Import(package, command.AdminId, ct);
    }
}
