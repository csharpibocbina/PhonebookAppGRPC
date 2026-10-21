using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Phonebook.Api.Auth;
using Phonebook.Api.Mapping;
using Phonebook.Api.Protos.Phonebook;
using Phonebook.Application.Contacts;

namespace Phonebook.Api.Services;

/// <summary>
/// The phonebook gRPC service. Kept thin on purpose:
/// proto request -> C# input -> application service -> proto response.
/// [Authorize] works exactly like on a controller: no valid JWT means the call
/// fails with StatusCode.Unauthenticated before reaching these methods.
/// </summary>
[Authorize]
public sealed class PhonebookGrpcService(IContactService contacts) : PhonebookService.PhonebookServiceBase
{
    public override async Task<Contact> CreateContact(CreateContactRequest request, ServerCallContext context)
    {
        var contact = await contacts.CreateAsync(context.GetUserId(), request.ToInput(), context.CancellationToken);
        return contact.ToProto();
    }

    public override async Task<Contact> GetContact(GetContactRequest request, ServerCallContext context)
    {
        var contact = await contacts.GetAsync(context.GetUserId(), ProtoMapper.ParseId(request.Id), context.CancellationToken);
        return contact.ToProto();
    }

    public override async Task<ListContactsResponse> ListContacts(ListContactsRequest request, ServerCallContext context)
    {
        var page = await contacts.ListAsync(context.GetUserId(), request.ToQuery(), context.CancellationToken);

        var response = new ListContactsResponse
        {
            TotalCount = page.TotalCount,
            Page = page.Page,
            PageSize = page.PageSize,
        };
        // "repeated" fields are exposed as a read-only RepeatedField<T>; add to it, don't assign it.
        response.Contacts.AddRange(page.Items.Select(c => c.ToProto()));
        return response;
    }

    public override async Task<Contact> UpdateContact(UpdateContactRequest request, ServerCallContext context)
    {
        var contact = await contacts.UpdateAsync(
            context.GetUserId(), ProtoMapper.ParseId(request.Id), request.ToInput(), context.CancellationToken);
        return contact.ToProto();
    }

    public override async Task<DeleteContactResponse> DeleteContact(DeleteContactRequest request, ServerCallContext context)
    {
        await contacts.DeleteAsync(context.GetUserId(), ProtoMapper.ParseId(request.Id), context.CancellationToken);
        return new DeleteContactResponse();
    }
}
