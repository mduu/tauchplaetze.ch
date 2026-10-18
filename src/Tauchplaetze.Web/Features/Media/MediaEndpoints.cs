using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Media.Queries;
using Tauchplaetze.Web.Features.Accounts;

namespace Tauchplaetze.Web.Features.Media;

internal static class MediaEndpoints
{
    /// <summary>Pfad der Vorschau privater Fotos im CMS, mit `{id}` und `{file}`.</summary>
    public const string PendingPathTemplate = "/admin/medien/{id}/{file}";

    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/admin/medien/{id:guid}/{file}", async (Guid id, string file, HttpContext context, IQueryHandler<GetPendingMediaFileQuery, MediaFile?> files) =>
        {
            var media = await files.Handle(new GetPendingMediaFileQuery(AccountCookie.UserId(context.User)!, id, file), context.RequestAborted);
            context.Response.Headers.CacheControl = "private, no-store";
            return media is null ? Results.NotFound() : Results.File(media.Content, media.ContentType);
        }).RequireAuthorization();

        return endpoints;
    }
}
