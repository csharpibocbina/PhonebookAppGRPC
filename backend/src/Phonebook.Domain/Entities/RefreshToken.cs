namespace Phonebook.Domain.Entities;

/// <summary>
/// A refresh token as stored in the database. We only store a SHA-256 hash,
/// never the token itself, so a database leak doesn't hand out valid tokens.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// All tokens created from one login share a family. If a token that was
    /// already rotated is used again, the whole family is revoked (likely theft).
    /// </summary>
    public Guid FamilyId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }

    public bool IsRevoked => RevokedAt is not null;
    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);
}
