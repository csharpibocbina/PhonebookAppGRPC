using FluentValidation;
using Phonebook.Application.Common;
using Phonebook.Application.Notifications;
using Phonebook.Domain.Entities;

namespace Phonebook.Application.Contacts;

public interface IContactService
{
    Task<Contact> CreateAsync(Guid ownerId, ContactInput input, CancellationToken cancellationToken = default);
    Task<Contact> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<Contact>> ListAsync(Guid ownerId, ContactListQuery query, CancellationToken cancellationToken = default);
    Task<Contact> UpdateAsync(Guid ownerId, Guid id, ContactInput input, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// The phonebook use cases. Ordinary C#: no gRPC types in here, so it can be
/// unit-tested with fakes and reused by any transport (gRPC, REST, ...).
/// </summary>
public sealed class ContactService(
    IContactRepository contacts,
    IUnitOfWork unitOfWork,
    IValidator<ContactInput> validator,
    INotificationPublisher notifications,
    TimeProvider time) : IContactService
{
    public async Task<Contact> CreateAsync(Guid ownerId, ContactInput input, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var now = time.GetUtcNow();
        var contact = new Contact { OwnerId = ownerId, CreatedAt = now, UpdatedAt = now };
        Apply(contact, input);

        contacts.Add(contact);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifications.PublishAsync(ownerId,
            Notification.Success(NotificationTypes.ContactCreated, "Contact added", $"{contact.FullName} was added to your phonebook.", time),
            cancellationToken);
        return contact;
    }

    public async Task<Contact> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken = default) =>
        await contacts.GetAsync(ownerId, id, cancellationToken) ?? throw NotFound(id);

    public async Task<PagedResult<Contact>> ListAsync(Guid ownerId, ContactListQuery query, CancellationToken cancellationToken = default)
    {
        query = query.Normalize();
        var (items, total) = await contacts.ListAsync(ownerId, query, cancellationToken);
        return new PagedResult<Contact>(items, total, query.Page, query.PageSize);
    }

    public async Task<Contact> UpdateAsync(Guid ownerId, Guid id, ContactInput input, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(input, cancellationToken);

        var contact = await contacts.GetAsync(ownerId, id, cancellationToken) ?? throw NotFound(id);
        Apply(contact, input);
        contact.UpdatedAt = time.GetUtcNow();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifications.PublishAsync(ownerId,
            Notification.Info(NotificationTypes.ContactUpdated, "Contact updated", $"{contact.FullName} was updated.", time),
            cancellationToken);
        return contact;
    }

    public async Task DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken = default)
    {
        var contact = await contacts.GetAsync(ownerId, id, cancellationToken) ?? throw NotFound(id);
        contacts.Remove(contact);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifications.PublishAsync(ownerId,
            Notification.Warning(NotificationTypes.ContactDeleted, "Contact deleted", $"{contact.FullName} was removed from your phonebook.", time),
            cancellationToken);
    }

    private static void Apply(Contact contact, ContactInput input)
    {
        contact.FirstName = input.FirstName.Trim();
        contact.LastName = input.LastName.Trim();
        contact.PhoneNumber = input.PhoneNumber.Trim();
        contact.Email = NullIfEmpty(input.Email);
        contact.Address = NullIfEmpty(input.Address);
        contact.Notes = NullIfEmpty(input.Notes);
        contact.IsFavorite = input.IsFavorite;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Same error whether the contact doesn't exist or belongs to someone else,
    // so callers can't probe for other users' contact ids.
    private static NotFoundException NotFound(Guid id) => new($"Contact '{id}' was not found.");
}
