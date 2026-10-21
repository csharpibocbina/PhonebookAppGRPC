using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Phonebook.Api.Protos.Auth;
using Phonebook.Api.Protos.Phonebook;
using Phonebook.Application.Auth;
using Phonebook.Application.Contacts;
using ContactEntity = Phonebook.Domain.Entities.Contact;
using UserEntity = Phonebook.Domain.Entities.User;

namespace Phonebook.Api.Mapping;

/// <summary>
/// Converts between generated protobuf messages (the wire format) and our own
/// C# types. Keeping this in one place means the rest of the code never touches
/// protobuf types.
/// </summary>
public static class ProtoMapper
{
    public static Contact ToProto(this ContactEntity c) => new()
    {
        Id = c.Id.ToString(),
        FirstName = c.FirstName,
        LastName = c.LastName,
        PhoneNumber = c.PhoneNumber,
        // proto3 strings can't be null; "" means "not set".
        Email = c.Email ?? string.Empty,
        Address = c.Address ?? string.Empty,
        Notes = c.Notes ?? string.Empty,
        IsFavorite = c.IsFavorite,
        CreatedAt = Timestamp.FromDateTimeOffset(c.CreatedAt),
        UpdatedAt = Timestamp.FromDateTimeOffset(c.UpdatedAt),
    };

    public static ContactInput ToInput(this CreateContactRequest r) =>
        new(r.FirstName, r.LastName, r.PhoneNumber, r.Email, r.Address, r.Notes, r.IsFavorite);

    public static ContactInput ToInput(this UpdateContactRequest r) =>
        new(r.FirstName, r.LastName, r.PhoneNumber, r.Email, r.Address, r.Notes, r.IsFavorite);

    public static ContactListQuery ToQuery(this ListContactsRequest r) =>
        new(r.Page, r.PageSize, r.Search, (ContactSortBy)(int)r.SortBy, r.FavoritesOnly);

    public static UserProfile ToProto(this UserEntity u) => new()
    {
        Id = u.Id.ToString(),
        Email = u.Email,
        DisplayName = u.DisplayName,
    };

    public static AuthResponse ToProto(this AuthResult result, bool includeRefreshToken) => new()
    {
        AccessToken = result.AccessToken,
        AccessTokenExpiresAt = result.AccessTokenExpiresAt.ToUnixTimeSeconds(),
        RefreshToken = includeRefreshToken ? result.RefreshToken : string.Empty,
        RefreshTokenExpiresAt = result.RefreshTokenExpiresAt.ToUnixTimeSeconds(),
        User = result.User.ToProto(),
    };

    public static Guid ParseId(string id) =>
        Guid.TryParse(id, out var guid)
            ? guid
            : throw new RpcException(new Status(StatusCode.InvalidArgument, $"'{id}' is not a valid contact id."));
}
