namespace JobFit.IntegrationTests;

public class AppHostHealthTests
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);

    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    public async Task Health_endpoint_returns_200_with_real_database(string path)
    {
        using var cts = new CancellationTokenSource(StartupTimeout);
        var cancellationToken = cts.Token;

        // The default provider is OpenAI; health checks never call the model, so any key works.
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.JobFit_AppHost>(
            ["Parameters:openai-api-key=integration-test-key"], cancellationToken);
        await using var app = await appHost.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);

        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", cancellationToken);

        using var httpClient = app.CreateHttpClient("api", "http");
        using var response = await httpClient.GetAsync(path, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
