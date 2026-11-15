using Microsoft.Extensions.AI;

namespace JobFit.Api.Tests;

// Deterministic stand-in for the embedding model: each input gets a normalized vector derived from its words.
public sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public const string ModelId = "fake-embedding-model";
    public const int DefaultDimensions = 768;

    public int Dimensions { get; set; } = DefaultDimensions;

    public Exception? Error { get; set; }

    public int CallCount { get; private set; }

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        CallCount++;
        if (Error is not null)
        {
            throw Error;
        }

        var inputs = values.ToList();
        var embeddings = new GeneratedEmbeddings<Embedding<float>>(
            inputs.Select(input => new Embedding<float>(Vectorize(input)) { ModelId = ModelId }))
        {
            Usage = new UsageDetails
            {
                InputTokenCount = inputs.Sum(input => input.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length),
            },
        };
        return Task.FromResult(embeddings);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType == typeof(EmbeddingGeneratorMetadata)
            ? new EmbeddingGeneratorMetadata("fake", null, ModelId, Dimensions)
            : null;

    public void Dispose()
    {
    }

    private float[] Vectorize(string input)
    {
        var vector = new float[Dimensions];
        foreach (var word in input.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            vector[(int)((uint)StringComparer.Ordinal.GetHashCode(word.ToLowerInvariant()) % (uint)Dimensions)] += 1;
        }

        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        if (norm == 0)
        {
            vector[0] = 1;
            return vector;
        }

        return vector.Select(v => v / norm).ToArray();
    }
}
