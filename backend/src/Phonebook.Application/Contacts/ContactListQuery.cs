namespace Phonebook.Application.Contacts;

public enum ContactSortBy
{
    Unspecified = 0,
    FirstName = 1,
    LastName = 2,
    CreatedAt = 3,
}

public sealed record ContactListQuery(
    int Page = 1,
    int PageSize = ContactListQuery.DefaultPageSize,
    string? Search = null,
    ContactSortBy SortBy = ContactSortBy.Unspecified,
    bool FavoritesOnly = false)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Clamps paging values so a client can't ask for page 0 or 10,000 rows.</summary>
    public ContactListQuery Normalize() => this with
    {
        Page = Page < 1 ? 1 : Page,
        PageSize = PageSize < 1 ? DefaultPageSize : Math.Min(PageSize, MaxPageSize),
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
    };
}
