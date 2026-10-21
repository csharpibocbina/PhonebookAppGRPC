using Grpc.Health.V1;

namespace Phonebook.IntegrationTests;

public sealed class HealthTests(PhonebookApiFactory factory) : IClassFixture<PhonebookApiFactory>
{
    [Fact]
    public async Task Grpc_health_service_reports_serving()
    {
        var client = new Health.HealthClient(factory.CreateChannel());

        var response = await client.CheckAsync(new HealthCheckRequest(), cancellationToken: TestContext.Current.CancellationToken);

        response.Status.ShouldBe(HealthCheckResponse.Types.ServingStatus.Serving);
    }
}
