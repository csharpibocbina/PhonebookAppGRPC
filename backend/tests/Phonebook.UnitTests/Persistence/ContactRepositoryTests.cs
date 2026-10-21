using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Phonebook.Application.Contacts;
using Phonebook.Domain.Entities;
using Phonebook.Infrastructure.Persistence;
using Phonebook.Infrastructure.Persistence.Repositories;
using Phonebook.UnitTests.TestHelpers;

namespace Phonebook.UnitTests.Persistence;

/// <summary>Runs the real EF Core queries against an in-memory SQLite database.</summary>
public sealed class ContactRepositoryTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AppDbContext _db = null!;
    private ContactRepository _sut = null!;
    private readonly User _owner = TestData.User("owner@example.com");
    private readonly User _other = TestData.User("other@example.com");

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        _db.Users.AddRange(_owner, _other);
        var now = TestData.Now;
        _db.Contacts.AddRange(
            Make(_owner, "Maria", "Santos", "+63 917 555 0101", "maria@example.com", fav: true, now.AddMinutes(1)),
            Make(_owner, "John", "Reyes", "+63 918 555 0102", null, fav: false, now.AddMinutes(2)),
            Make(_owner, "Ana", "Cruz", "+63 919 555 0103", "ana@work.com", fav: true, now.AddMinutes(3)),
            Make(_other, "Maria", "Other", "+1 555 0000", null, fav: true, now));
        await _db.SaveChangesAsync();

        _sut = new ContactRepository(_db);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Only_returns_the_owners_contacts()
    {
        var (items, total) = await _sut.ListAsync(_owner.Id, new ContactListQuery());

        total.ShouldBe(3);
        items.ShouldAllBe(c => c.OwnerId == _owner.Id);
    }

    [Theory]
    [InlineData("maria", 1)]
    [InlineData("MARIA", 1)]
    [InlineData("918", 1)]
    [InlineData("work.com", 1)]
    [InlineData("a", 2)]
    [InlineData("zzz", 0)]
    public async Task Search_matches_name_phone_or_email_case_insensitively(string search, int expected)
    {
        var (_, total) = await _sut.ListAsync(_owner.Id, new ContactListQuery(Search: search).Normalize());

        total.ShouldBe(expected);
    }

    [Fact]
    public async Task Favorites_filter_and_sorting_work()
    {
        var (items, _) = await _sut.ListAsync(_owner.Id, new ContactListQuery(FavoritesOnly: true, SortBy: ContactSortBy.LastName));

        items.Select(c => c.LastName).ShouldBe(["Cruz", "Santos"]);
    }

    [Fact]
    public async Task Sorting_by_created_at_returns_newest_first()
    {
        var (items, _) = await _sut.ListAsync(_owner.Id, new ContactListQuery(SortBy: ContactSortBy.CreatedAt));

        items.Select(c => c.FirstName).ShouldBe(["Ana", "John", "Maria"]);
    }

    [Fact]
    public async Task Paging_skips_and_takes()
    {
        var (items, total) = await _sut.ListAsync(_owner.Id, new ContactListQuery(Page: 2, PageSize: 2));

        total.ShouldBe(3);
        items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Get_does_not_return_someone_elses_contact()
    {
        var foreign = await _db.Contacts.SingleAsync(c => c.OwnerId == _other.Id);

        (await _sut.GetAsync(_owner.Id, foreign.Id)).ShouldBeNull();
        (await _sut.GetAsync(_other.Id, foreign.Id)).ShouldNotBeNull();
    }

    private static Contact Make(User owner, string first, string last, string phone, string? email, bool fav, DateTimeOffset created) => new()
    {
        OwnerId = owner.Id, FirstName = first, LastName = last, PhoneNumber = phone, Email = email,
        IsFavorite = fav, CreatedAt = created, UpdatedAt = created,
    };
}
