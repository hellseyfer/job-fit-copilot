using System.Numerics.Tensors;
using JobFit.Api.Knowledge;
using Microsoft.Extensions.AI;
using Xunit.Abstractions;

namespace JobFit.Evals;

// Checks the real embedding model. Skipped unless OPENAI_API_KEY or OLLAMA_ENDPOINT is set (or fails when JOBFIT_EVAL_REQUIRED=true).
[Collection("Evals")]
public class EmbeddingEvals(ITestOutputHelper output)
{
    [SkippableFact]
    public async Task Embeddings_have_the_store_dimensions_and_rank_related_text_closer()
    {
        using var generator = EvalHarness.RequireEmbeddingGenerator();
        var model = EvalHarness.EmbeddingModelName();

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
        var margin = (double)(related - unrelated);
        output.WriteLine($"related={related:F3} unrelated={unrelated:F3}");

        var passed = related > unrelated;
        EvalReport.Record("embeddings", "related minus unrelated cosine", model, margin, "> 0.00", passed);
        Assert.True(passed);
    }

    internal static IEmbeddingGenerator<string, Embedding<float>>? CreateEmbeddingGenerator() =>
        EvalHarness.CreateEmbeddingGenerator();
}
