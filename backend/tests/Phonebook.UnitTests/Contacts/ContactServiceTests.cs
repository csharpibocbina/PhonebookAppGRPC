using FluentValidation;
using Phonebook.Application.Common;
using Phonebook.Application.Contacts;
using Phonebook.Application.Notifications;
using Phonebook.Domain.Entities;
using Phonebook.UnitTests.TestHelpers;

namespace Phonebook.UnitTests.Contacts;

public class ContactServiceTests
{
    private readonly IContactRepository _repository = A.Fake<IContactRepository>();
    private readonly IUnitOfWork _unitOfWork = A.Fake<IUnitOfWork>();
    private readonly INotificationPublisher _notifications = A.Fake<INotificationPublisher>();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly ContactService _sut;

    public ContactServiceTests()
    {
        // FakeItEasy returns a "dummy" for unconfigured calls. For Task<Contact?> that is a
        // non-null empty Contact, so say explicitly that by default nothing is found.
        A.CallTo(() => _repository.GetAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).Returns((Contact?)null);

        // The real validator: its rules are part of the behaviour we want to test.
        _sut = new ContactService(_repository, _unitOfWork, new ContactInputValidator(), _notifications, TestData.FixedTime());
    }

    [Fact]
    public async Task CreateAsync_saves_contact_and_publishes_notification()
    {
        var contact = await _sut.CreateAsync(_ownerId, TestData.ValidInput() with { FirstName = "  Maria " });

        contact.OwnerId.ShouldBe(_ownerId);
        contact.FirstName.ShouldBe("Maria");
        contact.CreatedAt.ShouldBe(TestData.Now);
        A.CallTo(() => _repository.Add(contact)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _notifications.PublishAsync(
                _ownerId,
                A<Notification>.That.Matches(n => n.Type == NotificationTypes.ContactCreated && n.Message.Contains("Maria Santos")),
                A<CancellationToken>._)).MustHaveHappenedOnceExactly());
    }

    [Fact]
    public async Task CreateAsync_turns_blank_optional_fields_into_null()
    {
        var contact = await _sut.CreateAsync(_ownerId, new ContactInput("Ana", "Cruz", "+639195550103", Email: "  ", Notes: ""));

        contact.Email.ShouldBeNull();
        contact.Notes.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_with_invalid_input_throws_and_saves_nothing()
    {
        await Should.ThrowAsync<ValidationException>(() =>
            _sut.CreateAsync(_ownerId, new ContactInput("", "", "abc")));

        A.CallTo(() => _repository.Add(A<Contact>._)).MustNotHaveHappened();
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustNotHaveHappened();
        A.CallTo(() => _notifications.PublishAsync(A<Guid>._, A<Notification>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GetAsync_returns_contact_from_repository()
    {
        var existing = TestData.Contact(_ownerId);
        A.CallTo(() => _repository.GetAsync(_ownerId, existing.Id, A<CancellationToken>._)).Returns(existing);

        var contact = await _sut.GetAsync(_ownerId, existing.Id);

        contact.ShouldBeSameAs(existing);
    }

    [Fact]
    public async Task GetAsync_for_missing_or_foreign_contact_throws_not_found()
    {
        await Should.ThrowAsync<NotFoundException>(() => _sut.GetAsync(_ownerId, Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateAsync_changes_fields_and_bumps_updated_at()
    {
        var existing = TestData.Contact(_ownerId);
        existing.UpdatedAt = TestData.Now.AddDays(-3);
        A.CallTo(() => _repository.GetAsync(_ownerId, existing.Id, A<CancellationToken>._)).Returns(existing);

        var updated = await _sut.UpdateAsync(_ownerId, existing.Id, TestData.ValidInput("Mariel"));

        updated.FirstName.ShouldBe("Mariel");
        updated.IsFavorite.ShouldBeTrue();
        updated.UpdatedAt.ShouldBe(TestData.Now);
        A.CallTo(() => _unitOfWork.SaveChangesAsync(A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _notifications.PublishAsync(_ownerId,
            A<Notification>.That.Matches(n => n.Type == NotificationTypes.ContactUpdated), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task UpdateAsync_for_missing_contact_throws_not_found_and_does_not_notify()
    {
        await Should.ThrowAsync<NotFoundException>(() => _sut.UpdateAsync(_ownerId, Guid.NewGuid(), TestData.ValidInput()));

        A.CallTo(() => _notifications.PublishAsync(A<Guid>._, A<Notification>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task DeleteAsync_removes_contact_and_publishes_notification()
    {
        var existing = TestData.Contact(_ownerId);
        A.CallTo(() => _repository.GetAsync(_ownerId, existing.Id, A<CancellationToken>._)).Returns(existing);

        await _sut.DeleteAsync(_ownerId, existing.Id);

        A.CallTo(() => _repository.Remove(existing)).MustHaveHappenedOnceExactly();
        A.CallTo(() => _notifications.PublishAsync(_ownerId,
            A<Notification>.That.Matches(n => n.Type == NotificationTypes.ContactDeleted), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DeleteAsync_for_missing_contact_throws_not_found()
    {
        await Should.ThrowAsync<NotFoundException>(() => _sut.DeleteAsync(_ownerId, Guid.NewGuid()));

        A.CallTo(() => _repository.Remove(A<Contact>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task ListAsync_normalizes_paging_before_querying()
    {
        A.CallTo(() => _repository.ListAsync(_ownerId, A<ContactListQuery>._, A<CancellationToken>._))
            .Returns((new List<Contact> { TestData.Contact(_ownerId) }, 41));

        var page = await _sut.ListAsync(_ownerId, new ContactListQuery(Page: 0, PageSize: 500, Search: "  ma  "));

        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(ContactListQuery.MaxPageSize);
        page.TotalCount.ShouldBe(41);
        page.Items.Count.ShouldBe(1);
        A.CallTo(() => _repository.ListAsync(_ownerId,
            A<ContactListQuery>.That.Matches(q => q.Page == 1 && q.PageSize == 100 && q.Search == "ma"),
            A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }
}
