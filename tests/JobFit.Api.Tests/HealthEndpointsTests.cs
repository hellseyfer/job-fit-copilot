using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JobFit.Api.Tests;

public class HealthEndpointsTests(UnreachableDatabaseApiFactory factory) : IClassFixture<UnreachableDatabaseApiFactory>
{
    [Fact]
    public async Task Alive_returns_200_when_database_is_unreachable()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/alive");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_returns_503_when_database_is_unreachable()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Health_endpoints_are_available_in_production()
    {
        using var client = factory.CreateClient();
        var environment = factory.Services.GetRequiredService<IHostEnvironment>();

        using var response = await client.GetAsync("/alive");

        Assert.True(environment.IsProduction());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
