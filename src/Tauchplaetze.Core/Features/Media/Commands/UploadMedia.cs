using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Features.Media.Commands;

/// <summary>
/// Lädt ein Foto zu einem Platz hoch (`cms.md`, «Medien»). Wer hochlädt, bestätigt die Rechte. Das Foto bleibt privat,
/// bis eine freigegebene Version es verwendet. Gespeichert werden das Original (privat) und die Bildgrössen als WebP ohne Metadaten.
/// <paramref name="ContentType"/> ist nur ein Vorfilter: Das Format kommt aus dem Inhalt der Datei.
/// </summary>
public record UploadMediaCommand(
    string UploaderId,
    Guid DiveSiteId,
    byte[] Content,
    string ContentType,
    string Photographer,
    string? PhotographerUrl,
    MediaSection Section,
    int? Depth,
    bool RightsConfirmed,
    IReadOnlyList<MediaText> Texts);

internal class UploadMediaHandler(TauchplaetzeDbContext dbContext, IMediaStore store, AccountRoles roles, TimeProvider time)
    : ICommandHandler<UploadMediaCommand, UploadMediaResult>
{
    private static readonly HashSet<string> ContentTypes = new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

    public async Task<UploadMediaResult> Handle(UploadMediaCommand command, CancellationToken ct = default)
    {
        var rights = await roles.Of(command.UploaderId);
        if (!rights.CanWrite)
        {
            return Fail(EditorialError.Blocked);
        }

        if (!await dbContext.DiveSites.AnyAsync(d => d.Id == command.DiveSiteId && d.Status != DiveSiteStatus.Archived, ct))
        {
            return Fail(EditorialError.NotFound);
        }

        var errors = Errors(command);
        if (errors.Count > 0)
        {
            return new UploadMediaResult(errors);
        }

        if (!rights.IsModerator && await UploadQuota.Remaining(dbContext, time.GetUtcNow(), command.UploaderId, ct) == 0)
        {
            return Fail(EditorialError.TooManyUploads);
        }

        var image = await ImageProcessor.Process(command.Content, ct);
        if (image is null)
        {
            return Fail(EditorialError.InvalidImage);
        }

        var id = Guid.CreateVersion7(time.GetUtcNow());
        try
        {
            await store.SavePending(id, $"original.{image.Extension}", command.Content, image.ContentType, ct);
            foreach (var (width, content) in image.Renditions)
            {
                await store.SavePending(id, MediaFiles.Rendition(width), content, "image/webp", ct);
            }

            dbContext.MediaItems.Add(ToEntity(command, id, image));
            await dbContext.SaveChangesAsync(ct);
        }
        catch (Exception)
        {
            await store.DeletePending(id, CancellationToken.None);
            throw;
        }

        return new UploadMediaResult([], id);
    }

    private MediaItem ToEntity(UploadMediaCommand command, Guid id, ProcessedImage image) => new()
    {
        Id = id,
        DiveSiteId = command.DiveSiteId,
        Section = command.Section,
        Kind = MediaKind.Photo,
        BlobPath = id.ToString(),
        Width = image.Width,
        Height = image.Height,
        TakenAt = image.TakenAt,
        Depth = command.Depth,
        Photographer = command.Photographer.Trim(),
        PhotographerUrl = string.IsNullOrWhiteSpace(command.PhotographerUrl) ? null : command.PhotographerUrl.Trim(),
        UploadedBy = command.UploaderId,
        UploadedAt = time.GetUtcNow(),
        RightsConfirmed = true,
        Translations = MediaTexts.ToEntities(id, command.Texts),
    };

    private static List<EditorialError> Errors(UploadMediaCommand command)
    {
        var errors = new List<EditorialError>();
        if (!command.RightsConfirmed)
        {
            errors.Add(EditorialError.RightsNotConfirmed);
        }

        errors.AddRange(MediaRules.PhotographerErrors(command.Photographer, command.PhotographerUrl));

        if (!ContentTypes.Contains(command.ContentType) || command.Content.Length == 0 || command.Content.Length > MediaFiles.MaxBytes)
        {
            errors.Add(EditorialError.InvalidImage);
        }

        if (!Enum.IsDefined(command.Section) || command.Depth is < 0 or > 200 || !MediaTexts.AreValid(command.Texts))
        {
            errors.Add(EditorialError.InvalidMedia);
        }

        return errors;
    }

    private static UploadMediaResult Fail(EditorialError error) => new([error]);
}
