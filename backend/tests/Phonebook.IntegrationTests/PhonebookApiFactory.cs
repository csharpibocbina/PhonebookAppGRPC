using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Phonebook.Api.Protos.Auth;
using Phonebook.Api.Protos.Phonebook;

namespace Phonebook.IntegrationTests;

/// <summary>
/// Boots the whole API in memory (TestServer) with a throwaway SQLite database.
/// Tests talk to it with real gRPC clients over an in-memory HTTP/2 handler.
/// </summary>
public sealed class PhonebookApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"phonebook-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting applies before Program.cs reads configuration.
        builder.UseSetting("ConnectionStrings:Phonebook", $"Data Source={_databasePath}");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("Database:SeedDemoData", "false");
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-0123456789abcdef");
        builder.UseSetting("Jwt:RefreshCookieSecure", "false"); // TestServer is plain http
        builder.UseSetting("Smtp:Enabled", "false");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "10000");
    }

    /// <summary>A native gRPC channel (HTTP/2 + binary protobuf).</summary>
    public GrpcChannel CreateChannel() =>
        GrpcChannel.ForAddress(Server.BaseAddress, new GrpcChannelOptions { HttpHandler = Server.CreateHandler() });

    /// <summary>A gRPC-Web channel: the same protocol a browser uses.</summary>
    public GrpcChannel CreateGrpcWebChannel(HttpMessageHandler? inner = null) =>
        GrpcChannel.ForAddress(Server.BaseAddress, new GrpcChannelOptions
        {
            HttpHandler = new GrpcWebHandler(GrpcWebMode.GrpcWeb, inner ?? Server.CreateHandler()),
        });

    public AuthService.AuthServiceClient AuthClient() => new(CreateChannel());

    public PhonebookService.PhonebookServiceClient PhonebookClient() => new(CreateChannel());

    /// <summary>Registers a fresh user and returns its tokens.</summary>
    public async Task<AuthResponse> RegisterAsync(string? email = null)
    {
        email ??= $"user-{Guid.NewGuid():N}@example.com";
        return await AuthClient().RegisterAsync(new RegisterRequest { Email = email, Password = "Secret123", DisplayName = "Test User" });
    }

    /// <summary>gRPC metadata = HTTP headers. This is how the access token travels.</summary>
    public static Metadata Bearer(string accessToken) => new() { { "authorization", $"Bearer {accessToken}" } };

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }
}
