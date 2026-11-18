namespace FairShare.API.Middleware;

/// <summary>
/// Adds browser security headers to every response. The API only returns JSON, so the
/// browser is told to treat responses as exactly that: never sniff them into another type,
/// never run or frame them, never send the requesting URL on, and never cache them (group
/// data is reachable by anyone holding the share link, so it shouldn't linger on a shared
/// computer).
/// </summary>
/// <remarks>
/// Headers are set before the rest of the pipeline runs, so they're also on error (500) and
/// rate-limit (429) responses. The dev-only API reference pages (Scalar, OpenAPI JSON) load
/// their own scripts and styles, so they skip the content security policy.
/// </remarks>
public class SecurityHeadersMiddleware
{
    /// <summary>Nothing may load from a JSON response, and no page may frame it.</summary>
    public const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    private static readonly PathString[] DocsPaths = [new("/scalar"), new("/openapi")];

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions       = "DENY";
        headers["Referrer-Policy"]  = "no-referrer";

        var path = context.Request.Path;
        if (!DocsPaths.Any(p => path.StartsWithSegments(p)))
            headers.ContentSecurityPolicy = ApiContentSecurityPolicy;

        if (path.StartsWithSegments("/api"))
            headers.CacheControl = "no-store";

        return _next(context);
    }
}
