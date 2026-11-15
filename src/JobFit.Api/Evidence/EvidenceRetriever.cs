using System.Linq.Expressions;
using JobFit.Api.Knowledge;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace JobFit.Api.Evidence;

public sealed class EvidenceRetriever(
    KnowledgeCollection knowledge,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
{
    public async Task<SearchEvidenceResponse> SearchAsync(
        IReadOnlyList<(string Id, string Description)> requirements,
        string? kind,
        int top,
        CancellationToken cancellationToken)
    {
        var model = KnowledgeEmbeddings.ModelOf(embeddingGenerator);
        var vectors = await KnowledgeEmbeddings.GenerateAsync(
            embeddingGenerator, requirements.Select(r => r.Description).ToList(), cancellationToken);

        var collection = await knowledge.GetAsync(cancellationToken);
        var options = new VectorSearchOptions<KnowledgeChunkRecord> { Filter = FilterFor(model, kind) };

        var results = new List<RequirementEvidence>(requirements.Count);
        for (var i = 0; i < requirements.Count; i++)
        {
            var evidence = new List<EvidenceItem>(top);
            await foreach (var match in collection.SearchAsync(vectors[i], top, options, cancellationToken))
            {
                var chunk = match.Record;
                evidence.Add(new EvidenceItem(
                    chunk.DocumentId, chunk.Kind, chunk.Title, chunk.Section, chunk.ChunkIndex, chunk.Text,
                    Score: 1 - (match.Score ?? 1)));
            }

            results.Add(new RequirementEvidence(
                requirements[i].Id, evidence.OrderByDescending(e => e.Score).ToList()));
        }

        return new SearchEvidenceResponse(model, results);
    }

    // Vectors from different models are not comparable, so only chunks of the current model are searched.
    private static Expression<Func<KnowledgeChunkRecord, bool>> FilterFor(string model, string? kind) =>
        kind is null
            ? r => r.EmbeddingModel == model
            : r => r.EmbeddingModel == model && r.Kind == kind;
}
