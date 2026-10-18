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
    /// <summary>Höchstens so viele Uploads pro Stunde und Konto (`cms.md`, «Schutz gegen Missbrauch»). Moderation ausgenommen.</summary>
    public const int MaxPerHour = 30;

    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp",
    };

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

        var since = time.GetUtcNow().AddHours(-1);
        if (!rights.IsModerator && await dbContext.MediaItems.CountAsync(m => m.UploadedBy == command.UploaderId && m.UploadedAt > since, ct) >= MaxPerHour)
        {
            return Fail(EditorialError.TooManyUploads);
        }

        var image = ImageProcessor.Process(command.Content);
        if (image is null)
        {
            return Fail(EditorialError.InvalidImage);
        }

        var id = Guid.CreateVersion7(time.GetUtcNow());
        await store.SavePending(id, $"original.{Types[command.ContentType]}", command.Content, command.ContentType, ct);
        foreach (var (width, content) in image.Renditions)
        {
            await store.SavePending(id, MediaFiles.Rendition(width), content, "image/webp", ct);
        }

        dbContext.MediaItems.Add(new MediaItem
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
            PhotographerUrl = Blank(command.PhotographerUrl),
            UploadedBy = command.UploaderId,
            UploadedAt = time.GetUtcNow(),
            RightsConfirmed = true,
            Translations = MediaTexts.ToEntities(id, command.Texts),
        });
        await dbContext.SaveChangesAsync(ct);
        return new UploadMediaResult([], id);
    }

    private static List<EditorialError> Errors(UploadMediaCommand command)
    {
        var errors = new List<EditorialError>();
        if (!command.RightsConfirmed)
        {
            errors.Add(EditorialError.RightsNotConfirmed);
        }

        if (string.IsNullOrWhiteSpace(command.Photographer) || command.Photographer.Trim().Length > 120 || command.PhotographerUrl?.Trim().Length > 300)
        {
            errors.Add(EditorialError.InvalidPhotographer);
        }

        if (!Types.ContainsKey(command.ContentType) || command.Content.Length == 0 || command.Content.Length > MediaFiles.MaxBytes)
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

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
