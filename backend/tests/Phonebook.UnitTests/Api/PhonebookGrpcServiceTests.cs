using Grpc.Core;
using Phonebook.Api.Protos.Phonebook;
using Phonebook.Api.Services;
using Phonebook.Application.Common;
using Phonebook.Application.Contacts;
using Phonebook.UnitTests.TestHelpers;

namespace Phonebook.UnitTests.Api;

/// <summary>
/// A gRPC service is just a class: you can new it up and call its methods
/// directly with a test ServerCallContext. No server, no network.
/// </summary>
public class PhonebookGrpcServiceTests
{
    private readonly IContactService _contacts = A.Fake<IContactService>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly PhonebookGrpcService _sut;
    private readonly ServerCallContext _context;

    public PhonebookGrpcServiceTests()
    {
        _sut = new PhonebookGrpcService(_contacts);
        _context = TestServerCallContext.Create(TestData.HttpContextFor(_userId));
    }

    [Fact]
    public async Task CreateContact_maps_request_to_input_and_entity_to_proto()
    {
        var entity = TestData.Contact(_userId);
        A.CallTo(() => _contacts.CreateAsync(_userId, A<ContactInput>._, A<CancellationToken>._)).Returns(entity);

        var response = await _sut.CreateContact(new CreateContactRequest
        {
            FirstName = "Maria", LastName = "Santos", PhoneNumber = "+63 917 555 0101", IsFavorite = true,
        }, _context);

        response.Id.ShouldBe(entity.Id.ToString());
        response.FirstName.ShouldBe("Maria");
        response.Email.ShouldBe(string.Empty); // null becomes "" on the wire
        response.CreatedAt.ToDateTimeOffset().ShouldBe(TestData.Now);
        A.CallTo(() => _contacts.CreateAsync(_userId,
            A<ContactInput>.That.Matches(i => i.FirstName == "Maria" && i.IsFavorite), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ListContacts_returns_page_metadata_and_items()
    {
        A.CallTo(() => _contacts.ListAsync(_userId, A<ContactListQuery>._, A<CancellationToken>._))
            .Returns(new PagedResult<Domain.Entities.Contact>([TestData.Contact(_userId), TestData.Contact(_userId, "Ana")], 12, 2, 2));

        var response = await _sut.ListContacts(new ListContactsRequest { Page = 2, PageSize = 2, SortBy = SortBy.LastName }, _context);

        response.Contacts.Count.ShouldBe(2);
        response.TotalCount.ShouldBe(12);
        response.Page.ShouldBe(2);
        A.CallTo(() => _contacts.ListAsync(_userId,
            A<ContactListQuery>.That.Matches(q => q.SortBy == ContactSortBy.LastName), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Invalid_id_is_rejected_with_invalid_argument()
    {
        var ex = await Should.ThrowAsync<RpcException>(() => _sut.GetContact(new GetContactRequest { Id = "not-a-guid" }, _context));

        ex.StatusCode.ShouldBe(StatusCode.InvalidArgument);
        A.CallTo(() => _contacts.GetAsync(A<Guid>._, A<Guid>._, A<CancellationToken>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task DeleteContact_passes_the_signed_in_user_and_id()
    {
        var id = Guid.NewGuid();

        await _sut.DeleteContact(new DeleteContactRequest { Id = id.ToString() }, _context);

        A.CallTo(() => _contacts.DeleteAsync(_userId, id, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Missing_user_claim_is_unauthenticated()
    {
        var anonymous = TestServerCallContext.Create();

        var ex = await Should.ThrowAsync<RpcException>(() => _sut.ListContacts(new ListContactsRequest(), anonymous));

        ex.StatusCode.ShouldBe(StatusCode.Unauthenticated);
    }
}
