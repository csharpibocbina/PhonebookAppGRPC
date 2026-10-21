namespace Phonebook.Domain.Entities;

public class Contact
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The user who owns this contact. Users only ever see their own contacts.</summary>
    public Guid OwnerId { get; set; }
    public User? Owner { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool IsFavorite { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}
