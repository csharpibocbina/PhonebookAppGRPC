using System.Security.Claims;
using Grpc.Core;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Phonebook.Api.Auth;

public static class ServerCallContextExtensions
{
    /// <summary>
    /// gRPC runs on top of ASP.NET Core, so the authenticated user is on the
    /// normal HttpContext. The JWT middleware filled it from the
    /// "authorization: Bearer ..." metadata (gRPC metadata = HTTP headers).
    /// </summary>
    public static Guid GetUserId(this ServerCallContext context) =>
        context.GetHttpContext().User.GetUserId()
        ?? throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing user id in access token."));

    public static Guid? GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;
}
