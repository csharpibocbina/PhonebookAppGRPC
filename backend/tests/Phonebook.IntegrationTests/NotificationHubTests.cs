using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Phonebook.Api.Protos.Phonebook;
using Phonebook.Application.Notifications;
using static Phonebook.IntegrationTests.PhonebookApiFactory;

namespace Phonebook.IntegrationTests;

/// <summary>A gRPC call on one connection triggers a SignalR push on another.</summary>
public sealed class NotificationHubTests(PhonebookApiFactory factory) : IClassFixture<PhonebookApiFactory>
{
    private HubConnection CreateHubConnection(string? accessToken) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hubs/notifications"), options =>
            {
                // TestServer has no real sockets, so use long polling over its in-memory handler.
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                if (accessToken is not null)
                    options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .Build();

    [Fact]
    public async Task Creating_a_contact_pushes_a_notification_to_the_owner()
    {
        var ct = TestContext.Current.CancellationToken;
        var auth = await factory.RegisterAsync();
        await using var connection = CreateHubConnection(auth.AccessToken);
        var received = new TaskCompletionSource<Notification>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<Notification>("Notify", n =>
        {
            if (n.Type == NotificationTypes.ContactCreated)
                received.TrySetResult(n);
        });
        await connection.StartAsync(ct);

        await factory.PhonebookClient().CreateContactAsync(
            new CreateContactRequest { FirstName = "Live", LastName = "Update", PhoneNumber = "+63 900 333 4444" },
            Bearer(auth.AccessToken), cancellationToken: ct);

        var notification = await received.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        notification.Message.ShouldContain("Live Update");
        notification.Level.ShouldBe(NotificationLevels.Success);
    }

    [Fact]
    public async Task Other_users_do_not_receive_the_notification()
    {
        var ct = TestContext.Current.CancellationToken;
        var alice = await factory.RegisterAsync();
        var bob = await factory.RegisterAsync();
        await using var bobConnection = CreateHubConnection(bob.AccessToken);
        var bobGotSomething = false;
        bobConnection.On<Notification>("Notify", _ => bobGotSomething = true);
        await bobConnection.StartAsync(ct);

        await factory.PhonebookClient().CreateContactAsync(
            new CreateContactRequest { FirstName = "Private", PhoneNumber = "+63 900 555 6666" }, Bearer(alice.AccessToken), cancellationToken: ct);
        await Task.Delay(500, ct);

        bobGotSomething.ShouldBeFalse();
    }

    [Fact]
    public async Task Connecting_without_a_token_is_rejected()
    {
        await using var connection = CreateHubConnection(accessToken: null);

        var ex = await Should.ThrowAsync<HttpRequestException>(() => connection.StartAsync(TestContext.Current.CancellationToken));
        ex.StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized);
    }
}
