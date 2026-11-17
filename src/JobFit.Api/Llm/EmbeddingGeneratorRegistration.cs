using JobFit.Api.Knowledge;
using JobFit.Api.Observability;
using Microsoft.Extensions.AI;

namespace JobFit.Api.Llm;

public static class EmbeddingGeneratorRegistration
{
    public const string ConnectionName = "embeddings";

    public static IHostApplicationBuilder AddJobFitEmbeddingGenerator(this IHostApplicationBuilder builder)
    {
        var provider = builder.Configuration["Llm:Provider"] ?? "openai";

        // Keyed clients, so the embeddings connection does not replace the chat client registration.
        var generator = provider switch
        {
            "openai" => builder.AddKeyedOpenAIClient(ConnectionName, settings => settings.DisableTracing = true)
                .AddEmbeddingGenerator()
                // text-embedding-3 models can shorten their vectors to the store's fixed size.
                .ConfigureOptions(options => options.Dimensions ??= KnowledgeChunkRecord.Dimensions),
            "ollama" => builder.AddKeyedOllamaApiClient(ConnectionName, settings => settings.DisableTracing = true)
                .AddEmbeddingGenerator(),
            _ => throw new InvalidOperationException(
                $"Unsupported Llm:Provider '{provider}'. Use 'openai' or 'ollama'."),
        };

        generator.UseJobFitPipeline();
        return builder;
    }

    public static EmbeddingGeneratorBuilder<string, Embedding<float>> UseJobFitPipeline(
        this EmbeddingGeneratorBuilder<string, Embedding<float>> builder) =>
        builder
            .Use((inner, services) =>
            {
                var tracker = services.GetService<UsageTracker>();
                return tracker is null ? inner : new UsageTrackingEmbeddingGenerator(inner, tracker);
            })
            .UseOpenTelemetry();
}
