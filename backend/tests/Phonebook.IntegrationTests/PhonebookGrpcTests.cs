using Grpc.Core;
using Phonebook.Api.Protos.Phonebook;
using static Phonebook.IntegrationTests.PhonebookApiFactory;

namespace Phonebook.IntegrationTests;

public sealed class PhonebookGrpcTests(PhonebookApiFactory factory) : IClassFixture<PhonebookApiFactory>
{
    private readonly PhonebookService.PhonebookServiceClient _client = factory.PhonebookClient();

    [Fact]
    public async Task Full_crud_round_trip_over_grpc()
    {
        var auth = await factory.RegisterAsync();
        var headers = Bearer(auth.AccessToken);
        var ct = TestContext.Current.CancellationToken;

        // Create
        var created = await _client.CreateContactAsync(new CreateContactRequest
        {
            FirstName = "Maria", LastName = "Santos", PhoneNumber = "+63 917 555 0101", Email = "maria@example.com",
        }, headers, cancellationToken: ct);
        created.Id.ShouldNotBeNullOrEmpty();

        // Read
        var fetched = await _client.GetContactAsync(new GetContactRequest { Id = created.Id }, headers, cancellationToken: ct);
        fetched.Email.ShouldBe("maria@example.com");

        // Update
        var updated = await _client.UpdateContactAsync(new UpdateContactRequest
        {
            Id = created.Id, FirstName = "Mariel", LastName = "Santos", PhoneNumber = "+63 917 555 0101", IsFavorite = true,
        }, headers, cancellationToken: ct);
        updated.FirstName.ShouldBe("Mariel");
        updated.IsFavorite.ShouldBeTrue();

        // List
        var list = await _client.ListContactsAsync(new ListContactsRequest { Search = "mariel" }, headers, cancellationToken: ct);
        list.TotalCount.ShouldBe(1);

        // Delete
        await _client.DeleteContactAsync(new DeleteContactRequest { Id = created.Id }, headers, cancellationToken: ct);
        var ex = await Should.ThrowAsync<RpcException>(async () =>
            await _client.GetContactAsync(new GetContactRequest { Id = created.Id }, headers, cancellationToken: ct));
        ex.StatusCode.ShouldBe(StatusCode.NotFound);
    }

    [Fact]
    public async Task Calls_without_a_token_are_unauthenticated()
    {
        var ex = await Should.ThrowAsync<RpcException>(async () => await _client.ListContactsAsync(new ListContactsRequest()));

        ex.StatusCode.ShouldBe(StatusCode.Unauthenticated);
    }

    [Fact]
    public async Task Calls_with_a_garbage_token_are_unauthenticated()
    {
        var ex = await Should.ThrowAsync<RpcException>(async () =>
            await _client.ListContactsAsync(new ListContactsRequest(), Bearer("not.a.jwt")));

        ex.StatusCode.ShouldBe(StatusCode.Unauthenticated);
    }

    [Fact]
    public async Task Invalid_input_returns_invalid_argument_with_validation_trailer()
    {
        var auth = await factory.RegisterAsync();

        var ex = await Should.ThrowAsync<RpcException>(async () =>
            await _client.CreateContactAsync(new CreateContactRequest { FirstName = "", PhoneNumber = "abc" }, Bearer(auth.AccessToken)));

        ex.StatusCode.ShouldBe(StatusCode.InvalidArgument);
        var trailer = ex.Trailers.GetValue("validation-errors");
        trailer.ShouldNotBeNull();
        trailer.ShouldContain("firstName");
        trailer.ShouldContain("phoneNumber");
    }

    [Fact]
    public async Task Users_cannot_see_each_others_contacts()
    {
        var alice = await factory.RegisterAsync();
        var bob = await factory.RegisterAsync();
        var contact = await _client.CreateContactAsync(
            new CreateContactRequest { FirstName = "Secret", PhoneNumber = "+63 900 000 0000" }, Bearer(alice.AccessToken));

        var bobsList = await _client.ListContactsAsync(new ListContactsRequest(), Bearer(bob.AccessToken));
        bobsList.TotalCount.ShouldBe(0);

        var ex = await Should.ThrowAsync<RpcException>(async () =>
            await _client.DeleteContactAsync(new DeleteContactRequest { Id = contact.Id }, Bearer(bob.AccessToken)));
        ex.StatusCode.ShouldBe(StatusCode.NotFound);
    }
}
