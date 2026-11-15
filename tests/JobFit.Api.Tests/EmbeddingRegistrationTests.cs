using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace JobFit.Api.Tests;

public class EmbeddingRegistrationTests
{
    [Fact]
    public async Task Configured_embedding_generator_uses_the_shared_telemetry_pipeline()
    {
        await using var factory = new JobFitApiFactory();

        var generator = factory.Services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        Assert.NotNull(generator.GetService<OpenTelemetryEmbeddingGenerator<string, Embedding<float>>>());
        Assert.Equal("unused", generator.GetService<EmbeddingGeneratorMetadata>()?.DefaultModelId);
        Assert.NotNull(factory.Services.GetRequiredService<IChatClient>());
    }
}
