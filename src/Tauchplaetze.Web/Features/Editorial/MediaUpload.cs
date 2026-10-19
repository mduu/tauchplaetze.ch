using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Media;
using Tauchplaetze.Core.Features.Media.Commands;
using Tauchplaetze.Core.Features.Media.Queries;
using Tauchplaetze.Web.Localization;

namespace Tauchplaetze.Web.Features.Editorial;

/// <summary>
/// Lädt die gewählten Dateien nacheinander über <see cref="UploadMediaCommand"/> hoch. Für den Bearbeiten-Modus und das CMS.
/// Prüft Anzahl und Limit, bevor eine Datei gelesen wird, und bricht beim Limit oder bei einer Sperre ab.
/// </summary>
internal static class MediaUpload
{
    public const int MaxFiles = 20;

    public record Outcome(IReadOnlyList<Guid> Uploaded, IReadOnlyList<string> Errors);

    public static async Task<Outcome> Run(
        IReadOnlyList<IBrowserFile> files,
        UploadAllowance allowance,
        Func<byte[], string, Task<UploadMediaResult>> upload,
        IStringLocalizer<AdminStrings> L)
    {
        if (files.Count > MaxFiles)
        {
            return new Outcome([], [L["Media_TooManyFiles", MaxFiles].Value]);
        }

        if (!allowance.Allows(files.Count))
        {
            return new Outcome([], [allowance.Blocked ? L["Error_Blocked"].Value : L["Media_Remaining", allowance.Remaining ?? 0].Value]);
        }

        var uploaded = new List<Guid>();
        var errors = new List<string>();
        foreach (var file in files)
        {
            if (file.Size > MediaFiles.MaxBytes)
            {
                errors.Add($"{file.Name}: {L["Error_InvalidImage"]}");
                continue;
            }

            byte[] content;
            try
            {
                content = await Read(file);
            }
            catch (IOException)
            {
                errors.Add($"{file.Name}: {L["Media_UploadAborted"]}");
                break;
            }

            var result = await upload(content, file.ContentType);
            if (result.MediaId is { } id)
            {
                uploaded.Add(id);
                continue;
            }

            errors.AddRange(result.Errors.Select(e => $"{file.Name}: {L[$"Error_{e}"]}"));
            if (result.Errors.Any(e => e is EditorialError.TooManyUploads or EditorialError.Blocked))
            {
                break;
            }
        }

        return new Outcome(uploaded, errors);
    }

    private static async Task<byte[]> Read(IBrowserFile file)
    {
        var content = new byte[file.Size];
        await using var stream = file.OpenReadStream(MediaFiles.MaxBytes);
        await stream.ReadExactlyAsync(content);
        return content;
    }
}
