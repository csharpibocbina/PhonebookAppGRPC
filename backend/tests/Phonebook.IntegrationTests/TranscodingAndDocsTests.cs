using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Phonebook.Api.Protos.Phonebook;
using static Phonebook.IntegrationTests.PhonebookApiFactory;

namespace Phonebook.IntegrationTests;

/// <summary>The same C# methods, called as REST/JSON through gRPC JSON transcoding.</summary>
public sealed class TranscodingAndDocsTests(PhonebookApiFactory factory) : IClassFixture<PhonebookApiFactory>
{
    [Fact]
    public async Task Contact_created_over_grpc_is_visible_over_rest()
    {
        var auth = await factory.RegisterAsync();
        await factory.PhonebookClient().CreateContactAsync(
            new CreateContactRequest { FirstName = "Rest", LastName = "Check", PhoneNumber = "+63 900 111 2222" }, Bearer(auth.AccessToken));

        using var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var json = await http.GetFromJsonAsync<JsonElement>("/v1/contacts?search=rest", TestContext.Current.CancellationToken);

        json.GetProperty("totalCount").GetInt32().ShouldBe(1);
        json.GetProperty("contacts")[0].GetProperty("firstName").GetString().ShouldBe("Rest");
    }

    [Fact]
    public async Task Rest_login_returns_json_tokens()
    {
        var email = $"rest-{Guid.NewGuid():N}@example.com";
        await factory.RegisterAsync(email);
        using var http = factory.CreateClient();

        var response = await http.PostAsJsonAsync("/v1/auth/login", new { email, password = "Secret123" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        json.GetProperty("accessToken").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Rest_without_token_is_401()
    {
        using var http = factory.CreateClient();

        var response = await http.GetAsync("/v1/contacts", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task OpenApi_document_lists_the_transcoded_routes()
    {
        using var http = factory.CreateClient();

        var doc = await http.GetStringAsync("/swagger/v1/swagger.json", TestContext.Current.CancellationToken);

        doc.ShouldContain("/v1/contacts/{id}");
        doc.ShouldContain("/v1/auth/login");
    }

    [Fact]
    public async Task Scalar_ui_is_served()
    {
        using var http = factory.CreateClient();

        var response = await http.GetAsync("/scalar/", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
