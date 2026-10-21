namespace Phonebook.Application.Contacts;

/// <summary>The editable fields of a contact, used for both create and update.</summary>
public sealed record ContactInput(
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? Email = null,
    string? Address = null,
    string? Notes = null,
    bool IsFavorite = false);
