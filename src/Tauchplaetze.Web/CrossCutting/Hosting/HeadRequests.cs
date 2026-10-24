namespace Tauchplaetze.Web.CrossCutting.Hosting;

/// <summary>
/// Beantwortet HEAD wie GET, ohne Body (`infrastructure.md`, «HEAD»). Razor-Komponenten kennen nur GET und POST und gäben sonst 405.
/// Link-Prüfer, manche Crawler und `curl -I` fragen mit HEAD.
/// </summary>
internal static class HeadRequests
{
    public static IApplicationBuilder UseHeadAsGet(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (!HttpMethods.IsHead(context.Request.Method))
            {
                await next();
                return;
            }

            var body = context.Response.Body;
            context.Request.Method = HttpMethods.Get;
            context.Response.Body = Stream.Null;
            try
            {
                await next();
            }
            finally
            {
                context.Response.Body = body;
                context.Request.Method = HttpMethods.Head;
            }
        });
}
