using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Phonebook.Application.Common;
using Phonebook.Application.Email;
using Phonebook.Application.Notifications;
using Phonebook.Domain.Entities;

namespace Phonebook.Application.Auth;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterCommand command, CancellationToken cancellationToken = default);
    Task<AuthResult> LoginAsync(LoginCommand command, CancellationToken cancellationToken = default);
    Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default);
    Task<User> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Access + refresh token flow:
/// 1. Login returns a short-lived JWT access token and a long-lived random refresh token.
/// 2. The client sends the access token on every call ("authorization: Bearer ..." metadata).
/// 3. When it expires, the client calls Refresh. The old refresh token is revoked and a new
///    pair is issued (rotation).
/// 4. If a revoked refresh token is used again, someone copied it: revoke the whole family.
/// </summary>
public sealed class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    ITokenService tokenService,
    IPasswordHasher passwordHasher,
    IValidator<RegisterCommand> registerValidator,
    IValidator<LoginCommand> loginValidator,
    IEmailQueue emailQueue,
    INotificationPublisher notifications,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider time,
    ILogger<AuthService> logger) : IAuthService
{
    private const string InvalidCredentials = "Invalid email or password.";
    private const string InvalidRefreshToken = "Your session has expired. Please sign in again.";

    public async Task<AuthResult> RegisterAsync(RegisterCommand command, CancellationToken cancellationToken = default)
    {
        await registerValidator.ValidateAndThrowAsync(command, cancellationToken);

        var email = NormalizeEmail(command.Email);
        if (await users.EmailExistsAsync(email, cancellationToken))
            throw new ConflictException("An account with this email already exists.");

        var user = new User
        {
            Email = email,
            DisplayName = command.DisplayName.Trim(),
            PasswordHash = passwordHasher.Hash(command.Password),
            CreatedAt = time.GetUtcNow(),
        };
        users.Add(user);

        var result = IssueTokens(user, familyId: Guid.NewGuid());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await emailQueue.QueueAsync(EmailTemplates.Welcome(user), cancellationToken);
        logger.LogInformation("Registered user {UserId}", user.Id);
        return result;
    }

    public async Task<AuthResult> LoginAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        await loginValidator.ValidateAndThrowAsync(command, cancellationToken);

        var user = await users.GetByEmailAsync(NormalizeEmail(command.Email), cancellationToken);
        // Same message for "no such user" and "wrong password" so attackers can't discover emails.
        if (user is null || !passwordHasher.Verify(user.PasswordHash, command.Password))
            throw new AuthenticationFailedException(InvalidCredentials);

        var result = IssueTokens(user, familyId: Guid.NewGuid());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Tabs the user already has open get a toast; the email goes to PaperCut.
        await notifications.PublishAsync(user.Id,
            Notification.Info(NotificationTypes.SignedIn, "New sign-in", "Your account was just signed in to.", time),
            cancellationToken);
        await emailQueue.QueueAsync(EmailTemplates.SignInAlert(user, time.GetUtcNow()), cancellationToken);
        return result;
    }

    public async Task<AuthResult> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new AuthenticationFailedException(InvalidRefreshToken);

        var stored = await refreshTokens.GetByHashAsync(tokenService.HashRefreshToken(refreshToken), cancellationToken);
        if (stored?.User is null)
            throw new AuthenticationFailedException(InvalidRefreshToken);

        var now = time.GetUtcNow();
        if (stored.IsRevoked)
        {
            // Reuse detection: this token was already swapped for a new one.
            logger.LogWarning("Refresh token reuse detected for user {UserId}; revoking family {FamilyId}", stored.UserId, stored.FamilyId);
            foreach (var token in await refreshTokens.GetActiveByFamilyAsync(stored.FamilyId, cancellationToken))
                token.RevokedAt = now;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw new AuthenticationFailedException(InvalidRefreshToken);
        }

        if (stored.IsExpired(now))
            throw new AuthenticationFailedException(InvalidRefreshToken);

        var result = IssueTokens(stored.User, stored.FamilyId, out var replacement);
        stored.RevokedAt = now;
        stored.ReplacedByTokenId = replacement.Id;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        var stored = await refreshTokens.GetByHashAsync(tokenService.HashRefreshToken(refreshToken), cancellationToken);
        if (stored is null || stored.IsRevoked)
            return;

        stored.RevokedAt = time.GetUtcNow();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<User> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException("User not found.");

    private AuthResult IssueTokens(User user, Guid familyId) => IssueTokens(user, familyId, out _);

    private AuthResult IssueTokens(User user, Guid familyId, out RefreshToken refreshTokenEntity)
    {
        var now = time.GetUtcNow();
        var accessToken = tokenService.CreateAccessToken(user);
        var rawRefreshToken = tokenService.GenerateRefreshToken();

        refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenService.HashRefreshToken(rawRefreshToken),
            FamilyId = familyId,
            CreatedAt = now,
            ExpiresAt = now.AddDays(jwtOptions.Value.RefreshTokenDays),
        };
        refreshTokens.Add(refreshTokenEntity);

        return new AuthResult(accessToken.Token, accessToken.ExpiresAt, rawRefreshToken, refreshTokenEntity.ExpiresAt, user);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
