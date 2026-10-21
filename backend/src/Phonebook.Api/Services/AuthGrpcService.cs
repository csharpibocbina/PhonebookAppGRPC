using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Phonebook.Api.Auth;
using Phonebook.Api.Mapping;
using Phonebook.Api.Protos.Auth;
using Phonebook.Application.Auth;

namespace Phonebook.Api.Services;

/// <summary>
/// The gRPC endpoint for auth. Grpc.Tools generated AuthService.AuthServiceBase
/// from auth.proto; we override one method per rpc. Each method receives the
/// request message plus a ServerCallContext (metadata, deadline, cancellation,
/// and the underlying HttpContext).
/// </summary>
public sealed class AuthGrpcService(IAuthService auth, RefreshTokenCookie cookie) : Protos.Auth.AuthService.AuthServiceBase
{
    public override async Task<AuthResponse> Register(RegisterRequest request, ServerCallContext context)
    {
        var result = await auth.RegisterAsync(
            new RegisterCommand(request.Email, request.Password, request.DisplayName), context.CancellationToken);
        return Respond(result, context);
    }

    public override async Task<AuthResponse> Login(LoginRequest request, ServerCallContext context)
    {
        var result = await auth.LoginAsync(new LoginCommand(request.Email, request.Password), context.CancellationToken);
        return Respond(result, context);
    }

    public override async Task<AuthResponse> Refresh(RefreshRequest request, ServerCallContext context)
    {
        var http = context.GetHttpContext();
        var token = string.IsNullOrEmpty(request.RefreshToken) ? cookie.Read(http) : request.RefreshToken;
        try
        {
            var result = await auth.RefreshAsync(token, context.CancellationToken);
            return Respond(result, context);
        }
        catch
        {
            cookie.Clear(http);
            throw;
        }
    }

    public override async Task<LogoutResponse> Logout(LogoutRequest request, ServerCallContext context)
    {
        var http = context.GetHttpContext();
        var token = string.IsNullOrEmpty(request.RefreshToken) ? cookie.Read(http) : request.RefreshToken;
        await auth.LogoutAsync(token, context.CancellationToken);
        cookie.Clear(http);
        return new LogoutResponse();
    }

    [Authorize]
    public override async Task<UserProfile> Me(MeRequest request, ServerCallContext context)
    {
        var user = await auth.GetProfileAsync(context.GetUserId(), context.CancellationToken);
        return user.ToProto();
    }

    private AuthResponse Respond(AuthResult result, ServerCallContext context)
    {
        var http = context.GetHttpContext();
        cookie.Set(http, result.RefreshToken, result.RefreshTokenExpiresAt);
        return result.ToProto(includeRefreshToken: !RefreshTokenCookie.IsBrowserCall(http));
    }
}
