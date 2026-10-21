using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Phonebook.Application.Auth;
using Phonebook.Infrastructure.Auth;
using Phonebook.UnitTests.TestHelpers;

namespace Phonebook.UnitTests.Auth;

public class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "unit-test-signing-key-0123456789abcdef",
        AccessTokenMinutes = 15,
    };

    private readonly JwtTokenService _sut = new(Microsoft.Extensions.Options.Options.Create(Options), TimeProvider.System);

    [Fact]
    public async Task Access_token_is_a_valid_jwt_with_user_claims()
    {
        var user = TestData.User();

        var token = _sut.CreateAccessToken(user);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = JwtTokenService.CreateSigningKey(Options.SigningKey),
        });
        result.IsValid.ShouldBeTrue();
        result.Claims[JwtRegisteredClaimNames.Sub].ShouldBe(user.Id.ToString());
        result.Claims[JwtRegisteredClaimNames.Email].ShouldBe(user.Email);
    }

    [Fact]
    public void Access_token_expires_after_configured_minutes()
    {
        var time = TestData.FixedTime();
        var sut = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(Options), time);

        var token = sut.CreateAccessToken(TestData.User());

        token.ExpiresAt.ShouldBe(TestData.Now.AddMinutes(15));
        new JsonWebToken(token.Token).ValidTo.ShouldBe(TestData.Now.AddMinutes(15).UtcDateTime);
    }

    [Fact]
    public void Refresh_tokens_are_long_and_unique()
    {
        var first = _sut.GenerateRefreshToken();
        var second = _sut.GenerateRefreshToken();

        first.Length.ShouldBeGreaterThanOrEqualTo(80);
        first.ShouldNotBe(second);
    }

    [Fact]
    public void Hashing_is_deterministic_and_does_not_return_the_token()
    {
        var hash = _sut.HashRefreshToken("abc");

        hash.ShouldBe(_sut.HashRefreshToken("abc"));
        hash.ShouldNotContain("abc");
        hash.Length.ShouldBe(64); // SHA-256 as hex
    }

    [Fact]
    public void Short_signing_key_is_rejected() =>
        Should.Throw<InvalidOperationException>(() => JwtTokenService.CreateSigningKey("too-short"));
}
