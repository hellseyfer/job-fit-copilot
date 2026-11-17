using Microsoft.Extensions.AI;

namespace JobFit.Api.Observability;

public sealed class UsageTrackingEmbeddingGenerator(
    IEmbeddingGenerator<string, Embedding<float>> innerGenerator,
    UsageTracker tracker)
    : DelegatingEmbeddingGenerator<string, Embedding<float>>(innerGenerator)
{
    public override async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = await base.GenerateAsync(values, options, cancellationToken);
        var model = embeddings.FirstOrDefault()?.ModelId ?? this.GetService<EmbeddingGeneratorMetadata>()?.DefaultModelId;
        var inputTokens = embeddings.Usage?.InputTokenCount ?? 0;
        var outputTokens = embeddings.Usage?.OutputTokenCount ?? 0;
        var totalTokens = embeddings.Usage?.TotalTokenCount ?? inputTokens + outputTokens;
        tracker.RecordCall(model, inputTokens, outputTokens, totalTokens);
        return embeddings;
    }
}
