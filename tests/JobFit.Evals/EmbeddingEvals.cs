using System.ClientModel;
using System.Numerics.Tensors;
using JobFit.Api.Knowledge;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;
using Xunit.Abstractions;

namespace JobFit.Evals;

// Checks the real embedding model. Skipped unless OPENAI_API_KEY or OLLAMA_ENDPOINT is set.
public class EmbeddingEvals(ITestOutputHelper output)
{
    [SkippableFact]
    public async Task Embeddings_have_the_store_dimensions_and_rank_related_text_closer()
    {
        using var generator = CreateEmbeddingGenerator();
        Skip.If(generator is null, "Set OPENAI_API_KEY or OLLAMA_ENDPOINT to run the evals.");

        var embeddings = await generator.GenerateAsync(
        [
            "Built REST APIs in C# with ASP.NET Core and PostgreSQL.",
            "Backend development with .NET and SQL databases.",
            "Baked sourdough bread and pastries at a family bakery.",
        ],
        new EmbeddingGenerationOptions { Dimensions = KnowledgeChunkRecord.Dimensions });

        Assert.All(embeddings, e => Assert.Equal(KnowledgeChunkRecord.Dimensions, e.Vector.Length));
        var related = TensorPrimitives.CosineSimilarity(embeddings[0].Vector.Span, embeddings[1].Vector.Span);
        var unrelated = TensorPrimitives.CosineSimilarity(embeddings[0].Vector.Span, embeddings[2].Vector.Span);
        output.WriteLine($"related={related:F3} unrelated={unrelated:F3}");
        Assert.True(related > unrelated);
    }

    internal static IEmbeddingGenerator<string, Embedding<float>>? CreateEmbeddingGenerator()
    {
        var model = Environment.GetEnvironmentVariable("JOBFIT_EVAL_EMBEDDING_MODEL");

        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(openAiKey))
        {
            return new OpenAIClient(new ApiKeyCredential(openAiKey))
                .GetEmbeddingClient(string.IsNullOrEmpty(model) ? "text-embedding-3-small" : model)
                .AsIEmbeddingGenerator()
                .AsBuilder()
                // Same as the API: text-embedding-3 models shorten their vectors to the store's fixed size.
                .ConfigureOptions(options => options.Dimensions ??= KnowledgeChunkRecord.Dimensions)
                .Build();
        }

        var ollamaEndpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT");
        if (!string.IsNullOrEmpty(ollamaEndpoint))
        {
            return new OllamaApiClient(new Uri(ollamaEndpoint), string.IsNullOrEmpty(model) ? "nomic-embed-text" : model);
        }

        return null;
    }
}
