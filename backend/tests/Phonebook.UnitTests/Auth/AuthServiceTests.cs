using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Phonebook.Application.Auth;
using Phonebook.Application.Common;
using Phonebook.Application.Email;
using Phonebook.Application.Notifications;
using Phonebook.Domain.Entities;
using Phonebook.UnitTests.TestHelpers;

namespace Phonebook.UnitTests.Auth;

public class AuthServiceTests
{
    private readonly IUserRepository _users = A.Fake<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = A.Fake<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly ITokenService _tokens = A.Fake<ITokenService>();
    private readonly IPasswordHasher _hasher = A.Fake<IPasswordHasher>();
    private readonly IEmailQueue _emailQueue = A.Fake<IEmailQueue>();
    private readonly INotificationPublisher _notifications = A.Fake<INotificationPublisher>();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        A.CallTo(() => _tokens.CreateAccessToken(A<User>._)).Returns(new AccessToken("access-jwt", TestData.Now.AddMinutes(15)));
        A.CallTo(() => _tokens.GenerateRefreshToken()).ReturnsNextFromSequence("refresh-1", "refresh-2", "refresh-3");
        // Fake "hash": prefix the token so tests can tell raw and hashed values apart.
        A.CallTo(() => _tokens.HashRefreshToken(A<string>._)).ReturnsLazily((string token) => $"hash:{token}");
        // Unconfigured fakes return dummies (a non-null empty User), so default to "not found".
        A.CallTo(() => _users.GetByEmailAsync(A<string>._, A<CancellationToken>._)).Returns((User?)null);
        A.CallTo(() => _refreshTokens.GetByHashAsync(A<string>._, A<CancellationToken>._)).Returns((RefreshToken?)null);
        A.CallTo(() => _hasher.Hash(A<string>._)).ReturnsLazily((string password) => $"pbkdf2:{password}");

        _sut = new AuthService(
            _users, _refreshTokens, _unitOfWork, _tokens, _hasher,
            new RegisterCommandValidator(), new LoginCommandValidator(),
            _emailQueue, _notifications,
            Options.Create(new JwtOptions { RefreshTokenDays = 7 }),
            TestData.FixedTime(), NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task Register_creates_user_issues_tokens_and_queues_welcome_email()
    {
        User? added = null;
        A.CallTo(() => _users.Add(A<User>._)).Invokes((User u) => added = u);

        var result = await _sut.RegisterAsync(new RegisterCommand("  Jay@Example.com ", "Secret123", "Jay"));

        added.ShouldNotBeNull();
        added.Email.ShouldBe("jay@example.com");
        added.PasswordHash.ShouldBe("pbkdf2:Secret123");
        result.AccessToken.ShouldBe("access-jwt");
        result.RefreshToken.ShouldBe("refresh-1");
        result.RefreshTokenExpiresAt.ShouldBe(TestData.Now.AddDays(7));
        A.CallTo(() => _refreshTokens.Add(A<RefreshToken>.That.Matches(t => t.TokenHash == "hash:refresh-1" && t.UserId == added.Id)))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _emailQueue.QueueAsync(
            A<EmailMessage>.That.Matches(m => m.To == "jay@example.com" && m.Subject.Contains("Welcome")),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Register_with_existing_email_throws_conflict()
    {
        A.CallTo(() => _users.EmailExistsAsync("jay@example.com", A<CancellationToken>._)).Returns(true);

        await Should.ThrowAsync<ConflictException>(() => _sut.RegisterAsync(new RegisterCommand("jay@example.com", "Secret123", "Jay")));
        A.CallTo(() => _users.Add(A<User>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData("short1A")]
    [InlineData("alllowercase1")]
    [InlineData("NoDigitsHere")]
    public async Task Register_with_weak_password_throws_validation_error(string password)
    {
        await Should.ThrowAsync<ValidationException>(() => _sut.RegisterAsync(new RegisterCommand("jay@example.com", password, "Jay")));
    }

    [Fact]
    public async Task Login_with_correct_password_issues_tokens_notifies_and_emails()
    {
        var user = TestData.User();
        A.CallTo(() => _users.GetByEmailAsync("jay@example.com", A<CancellationToken>._)).Returns(user);
        A.CallTo(() => _hasher.Verify(user.PasswordHash, "Secret123")).Returns(true);

        var result = await _sut.LoginAsync(new LoginCommand("JAY@example.com", "Secret123"));

        result.User.ShouldBeSameAs(user);
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _notifications.PublishAsync(user.Id,
            A<Notification>.That.Matches(n => n.Type == NotificationTypes.SignedIn), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _emailQueue.QueueAsync(A<EmailMessage>.That.Matches(m => m.Subject.Contains("sign-in")), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Login_with_wrong_password_throws_authentication_failed()
    {
        var user = TestData.User();
        A.CallTo(() => _users.GetByEmailAsync(A<string>._, A<CancellationToken>._)).Returns(user);
        A.CallTo(() => _hasher.Verify(A<string>._, A<string>._)).Returns(false);

        var ex = await Should.ThrowAsync<AuthenticationFailedException>(() => _sut.LoginAsync(new LoginCommand("jay@example.com", "nope")));
        ex.Message.ShouldBe("Invalid email or password.");
        A.CallTo(() => _refreshTokens.Add(A<RefreshToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Login_with_unknown_email_gives_the_same_error_as_wrong_password()
    {
        var ex = await Should.ThrowAsync<AuthenticationFailedException>(() => _sut.LoginAsync(new LoginCommand("ghost@example.com", "x")));
        ex.Message.ShouldBe("Invalid email or password.");
    }

    [Fact]
    public async Task Refresh_rotates_the_token_in_the_same_family()
    {
        var user = TestData.User();
        var familyId = Guid.NewGuid();
        var stored = new RefreshToken { User = user, UserId = user.Id, FamilyId = familyId, TokenHash = "hash:old", ExpiresAt = TestData.Now.AddDays(1) };
        A.CallTo(() => _refreshTokens.GetByHashAsync("hash:old", A<CancellationToken>._)).Returns(stored);
        RefreshToken? replacement = null;
        A.CallTo(() => _refreshTokens.Add(A<RefreshToken>._)).Invokes((RefreshToken t) => replacement = t);

        var result = await _sut.RefreshAsync("old");

        result.RefreshToken.ShouldBe("refresh-1");
        stored.RevokedAt.ShouldBe(TestData.Now);
        replacement.ShouldNotBeNull();
        replacement.FamilyId.ShouldBe(familyId);
        stored.ReplacedByTokenId.ShouldBe(replacement.Id);
    }

    [Fact]
    public async Task Refresh_with_already_used_token_revokes_whole_family()
    {
        var user = TestData.User();
        var familyId = Guid.NewGuid();
        var reused = new RefreshToken { User = user, FamilyId = familyId, RevokedAt = TestData.Now.AddMinutes(-5), ExpiresAt = TestData.Now.AddDays(1) };
        var current = new RefreshToken { User = user, FamilyId = familyId, ExpiresAt = TestData.Now.AddDays(1) };
        A.CallTo(() => _refreshTokens.GetByHashAsync("hash:stolen", A<CancellationToken>._)).Returns(reused);
        A.CallTo(() => _refreshTokens.GetActiveByFamilyAsync(familyId, A<CancellationToken>._)).Returns([current]);

        await Should.ThrowAsync<AuthenticationFailedException>(() => _sut.RefreshAsync("stolen"));

        current.RevokedAt.ShouldBe(TestData.Now);
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _refreshTokens.Add(A<RefreshToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task Refresh_with_expired_token_fails()
    {
        var stored = new RefreshToken { User = TestData.User(), ExpiresAt = TestData.Now.AddSeconds(-1) };
        A.CallTo(() => _refreshTokens.GetByHashAsync(A<string>._, A<CancellationToken>._)).Returns(stored);

        await Should.ThrowAsync<AuthenticationFailedException>(() => _sut.RefreshAsync("expired"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    public async Task Refresh_with_missing_or_unknown_token_fails(string? token)
    {
        await Should.ThrowAsync<AuthenticationFailedException>(() => _sut.RefreshAsync(token));
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var stored = new RefreshToken { ExpiresAt = TestData.Now.AddDays(1) };
        A.CallTo(() => _refreshTokens.GetByHashAsync("hash:mine", A<CancellationToken>._)).Returns(stored);

        await _sut.LogoutAsync("mine");

        stored.RevokedAt.ShouldBe(TestData.Now);
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Logout_without_token_does_nothing()
    {
        await _sut.LogoutAsync(null);

        A.CallTo(() => _refreshTokens.GetByHashAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
    }
}
