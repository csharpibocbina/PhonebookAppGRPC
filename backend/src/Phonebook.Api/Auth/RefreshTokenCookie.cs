using Microsoft.Extensions.Options;
using Phonebook.Application.Auth;

namespace Phonebook.Api.Auth;

/// <summary>
/// The browser keeps the refresh token in an httpOnly cookie: JavaScript can't
/// read it, so an XSS bug can't steal it. gRPC-Web is plain fetch(), so the
/// browser sends the cookie automatically on same-origin calls.
/// </summary>
public sealed class RefreshTokenCookie(IOptions<JwtOptions> options)
{
    public const string Name = "phonebook_refresh";

    public void Set(HttpContext http, string token, DateTimeOffset expiresAt) =>
        http.Response.Cookies.Append(Name, token, CreateOptions(expiresAt));

    public string? Read(HttpContext http) => http.Request.Cookies.TryGetValue(Name, out var value) ? value : null;

    public void Clear(HttpContext http) => http.Response.Cookies.Delete(Name, CreateOptions(null));

    /// <summary>
    /// Browsers call us with gRPC-Web. For them the refresh token only lives in
    /// the cookie and is left out of the response body.
    /// </summary>
    public static bool IsBrowserCall(HttpContext http) => http.Items.ContainsKey(GrpcWebMarker);

    private const string GrpcWebMarker = "phonebook:grpc-web";

    /// <summary>
    /// UseGrpcWeb rewrites the content type to application/grpc, so remember
    /// whether the call arrived as gRPC-Web before that middleware runs.
    /// </summary>
    public static IApplicationBuilder UseGrpcWebDetection(IApplicationBuilder app) => app.Use((http, next) =>
    {
        if (http.Request.ContentType?.StartsWith("application/grpc-web", StringComparison.OrdinalIgnoreCase) == true)
            http.Items[GrpcWebMarker] = true;
        return next(http);
    });

    private CookieOptions CreateOptions(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = options.Value.RefreshCookieSecure,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = expiresAt,
        IsEssential = true,
    };
}
