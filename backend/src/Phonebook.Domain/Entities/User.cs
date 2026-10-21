namespace Phonebook.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public List<Contact> Contacts { get; set; } = [];
    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
