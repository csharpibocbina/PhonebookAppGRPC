using Phonebook.Domain.Entities;

namespace Phonebook.Application.Auth;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);

    /// <summary>A long random string. Not a JWT: it only means something to our database.</summary>
    string GenerateRefreshToken();

    string HashRefreshToken(string refreshToken);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string passwordHash, string password);
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    void Add(User user);
}

public interface IRefreshTokenRepository
{
    /// <summary>Finds a token by its hash and loads its user.</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RefreshToken>> GetActiveByFamilyAsync(Guid familyId, CancellationToken cancellationToken = default);
    void Add(RefreshToken token);
}
