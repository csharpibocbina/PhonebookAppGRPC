using Microsoft.AspNetCore.Identity;
using Phonebook.Domain.Entities;
using IAppPasswordHasher = Phonebook.Application.Auth.IPasswordHasher;

namespace Phonebook.Infrastructure.Auth;

/// <summary>Wraps ASP.NET Core Identity's PBKDF2 password hasher.</summary>
public sealed class PasswordHasher : IAppPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();
    private static readonly User Unused = new();

    public string Hash(string password) => _inner.HashPassword(Unused, password);

    public bool Verify(string passwordHash, string password) =>
        _inner.VerifyHashedPassword(Unused, passwordHash, password) != PasswordVerificationResult.Failed;
}
