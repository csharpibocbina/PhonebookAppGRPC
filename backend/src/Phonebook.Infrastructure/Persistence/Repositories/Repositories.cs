using Microsoft.EntityFrameworkCore;
using Phonebook.Application.Auth;
using Phonebook.Application.Contacts;
using Phonebook.Domain.Entities;

namespace Phonebook.Infrastructure.Persistence.Repositories;

internal sealed class ContactRepository(AppDbContext db) : IContactRepository
{
    public Task<Contact?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken = default) =>
        db.Contacts.FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == ownerId, cancellationToken);

    public async Task<(IReadOnlyList<Contact> Items, int TotalCount)> ListAsync(
        Guid ownerId, ContactListQuery query, CancellationToken cancellationToken = default)
    {
        var contacts = db.Contacts.AsNoTracking().Where(c => c.OwnerId == ownerId);

        if (query.FavoritesOnly)
            contacts = contacts.Where(c => c.IsFavorite);

        if (query.Search is { } search)
        {
            var term = search.ToLower();
            contacts = contacts.Where(c =>
                c.FirstName.ToLower().Contains(term) ||
                c.LastName.ToLower().Contains(term) ||
                c.PhoneNumber.Contains(term) ||
                (c.Email != null && c.Email.ToLower().Contains(term)));
        }

        var total = await contacts.CountAsync(cancellationToken);

        contacts = query.SortBy switch
        {
            ContactSortBy.LastName => contacts.OrderBy(c => c.LastName).ThenBy(c => c.FirstName),
            ContactSortBy.CreatedAt => contacts.OrderByDescending(c => c.CreatedAt),
            _ => contacts.OrderBy(c => c.FirstName).ThenBy(c => c.LastName),
        };

        var items = await contacts
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public void Add(Contact contact) => db.Contacts.Add(contact);

    public void Remove(Contact contact) => db.Contacts.Remove(contact);
}

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) =>
        db.Users.AnyAsync(u => u.Email == email, cancellationToken);

    public void Add(User user) => db.Users.Add(user);
}

internal sealed class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByFamilyAsync(Guid familyId, CancellationToken cancellationToken = default) =>
        await db.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null).ToListAsync(cancellationToken);

    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);
}
