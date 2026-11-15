using Microsoft.Extensions.AI;

namespace JobFit.Api.Knowledge;

public sealed class EmbeddingFailedException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public static class KnowledgeEmbeddings
{
    public static string ModelOf(IEmbeddingGenerator<string, Embedding<float>> generator) =>
        generator.GetService<EmbeddingGeneratorMetadata>()?.DefaultModelId ?? "unknown";

    // One batched call; fails unless every input gets a vector with the store's dimensions.
    public static async Task<List<ReadOnlyMemory<float>>> GenerateAsync(
        IEmbeddingGenerator<string, Embedding<float>> generator,
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken)
    {
        GeneratedEmbeddings<Embedding<float>> generated;
        try
        {
            generated = await generator.GenerateAsync(inputs, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EmbeddingFailedException("The embedding model call failed.", exception);
        }

        if (generated.Count != inputs.Count)
        {
            throw new EmbeddingFailedException(
                $"The embedding model returned {generated.Count} vectors for {inputs.Count} inputs.");
        }

        var mismatch = generated.FirstOrDefault(e => e.Vector.Length != KnowledgeChunkRecord.Dimensions);
        if (mismatch is not null)
        {
            throw new EmbeddingFailedException(
                $"The embedding model returned {mismatch.Vector.Length} dimensions; the store expects {KnowledgeChunkRecord.Dimensions}.");
        }

        return generated.Select(e => e.Vector).ToList();
    }
}
