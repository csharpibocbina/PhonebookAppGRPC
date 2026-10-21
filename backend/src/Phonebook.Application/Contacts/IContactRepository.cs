using Phonebook.Domain.Entities;

namespace Phonebook.Application.Contacts;

public interface IContactRepository
{
    /// <summary>Returns the contact only if it belongs to <paramref name="ownerId"/>.</summary>
    Task<Contact?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Contact> Items, int TotalCount)> ListAsync(
        Guid ownerId, ContactListQuery query, CancellationToken cancellationToken = default);

    void Add(Contact contact);
    void Remove(Contact contact);
}
