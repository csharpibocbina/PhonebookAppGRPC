using System.Net;
using Grpc.Core;
using Phonebook.Api.Protos.Auth;
using static Phonebook.IntegrationTests.PhonebookApiFactory;

namespace Phonebook.IntegrationTests;

public sealed class AuthGrpcTests(PhonebookApiFactory factory) : IClassFixture<PhonebookApiFactory>
{
    private readonly AuthService.AuthServiceClient _client = factory.AuthClient();

    [Fact]
    public async Task Register_then_login_then_me()
    {
        var email = $"jay-{Guid.NewGuid():N}@example.com";
        await factory.RegisterAsync(email);

        var login = await _client.LoginAsync(new LoginRequest { Email = email, Password = "Secret123" });
        login.AccessToken.ShouldNotBeNullOrEmpty();
        login.RefreshToken.ShouldNotBeNullOrEmpty(); // native gRPC clients get it in the body

        var me = await _client.MeAsync(new MeRequest(), Bearer(login.AccessToken));
        me.Email.ShouldBe(email);
    }

    [Fact]
    public async Task Duplicate_registration_is_already_exists()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        await factory.RegisterAsync(email);

        var ex = await Should.ThrowAsync<RpcException>(async () => await factory.RegisterAsync(email));
        ex.StatusCode.ShouldBe(StatusCode.AlreadyExists);
    }

    [Fact]
    public async Task Wrong_password_is_unauthenticated()
    {
        var email = $"wrong-{Guid.NewGuid():N}@example.com";
        await factory.RegisterAsync(email);

        var ex = await Should.ThrowAsync<RpcException>(async () =>
            await _client.LoginAsync(new LoginRequest { Email = email, Password = "Wrong1234" }));
        ex.StatusCode.ShouldBe(StatusCode.Unauthenticated);
    }

    [Fact]
    public async Task Refresh_rotates_and_detects_reuse()
    {
        var first = await factory.RegisterAsync();

        var second = await _client.RefreshAsync(new RefreshRequest { RefreshToken = first.RefreshToken });
        second.RefreshToken.ShouldNotBe(first.RefreshToken);

        // Re-using the first (already rotated) token is treated as theft...
        var ex = await Should.ThrowAsync<RpcException>(async () =>
            await _client.RefreshAsync(new RefreshRequest { RefreshToken = first.RefreshToken }));
        ex.StatusCode.ShouldBe(StatusCode.Unauthenticated);

        // ...and kills the newer token from the same family too.
        ex = await Should.ThrowAsync<RpcException>(async () =>
            await _client.RefreshAsync(new RefreshRequest { RefreshToken = second.RefreshToken }));
        ex.StatusCode.ShouldBe(StatusCode.Unauthenticated);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var auth = await factory.RegisterAsync();

        await _client.LogoutAsync(new LogoutRequest { RefreshToken = auth.RefreshToken });

        var ex = await Should.ThrowAsync<RpcException>(async () =>
            await _client.RefreshAsync(new RefreshRequest { RefreshToken = auth.RefreshToken }));
        ex.StatusCode.ShouldBe(StatusCode.Unauthenticated);
    }

    [Fact]
    public async Task Browser_grpc_web_calls_get_the_refresh_token_only_as_an_httponly_cookie()
    {
        var cookies = new CookieContainer();
        var cookieHandler = new CookieContainerHandler(cookies) { InnerHandler = factory.Server.CreateHandler() };
        var browser = new AuthService.AuthServiceClient(factory.CreateGrpcWebChannel(cookieHandler));
        var email = $"web-{Guid.NewGuid():N}@example.com";

        var registered = await browser.RegisterAsync(new RegisterRequest { Email = email, Password = "Secret123", DisplayName = "Web" });
        registered.RefreshToken.ShouldBeEmpty();

        var cookie = cookies.GetCookies(factory.Server.BaseAddress)["phonebook_refresh"];
        cookie.ShouldNotBeNull();
        cookie.HttpOnly.ShouldBeTrue();

        // Refresh with an empty body: the server reads the cookie.
        var refreshed = await browser.RefreshAsync(new RefreshRequest());
        refreshed.AccessToken.ShouldNotBeNullOrEmpty();
        cookies.GetCookies(factory.Server.BaseAddress)["phonebook_refresh"]!.Value.ShouldNotBe(cookie.Value);
    }

    /// <summary>Minimal cookie jar for TestServer, which has no built-in cookie support.</summary>
    private sealed class CookieContainerHandler(CookieContainer cookies) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var header = cookies.GetCookieHeader(request.RequestUri!);
            if (!string.IsNullOrEmpty(header))
                request.Headers.Add("Cookie", header);

            var response = await base.SendAsync(request, cancellationToken);
            if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
                foreach (var setCookie in setCookies)
                    cookies.SetCookies(request.RequestUri!, setCookie);
            return response;
        }
    }
}
