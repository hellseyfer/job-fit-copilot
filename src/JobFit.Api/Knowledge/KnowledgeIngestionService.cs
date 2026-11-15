using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace JobFit.Api.Knowledge;

public sealed record IngestionResult(KnowledgeDocumentResponse Document, bool Created);

public sealed class KnowledgeIngestionService(
    KnowledgeCollection knowledge,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    TimeProvider timeProvider)
{
    // Every chunk holds at least one character of the content, so this bounds the chunks of any valid document.
    private const int MaxChunksPerDocument = KnowledgeEndpoints.MaxContentLength;

    private string EmbeddingModel => KnowledgeEmbeddings.ModelOf(embeddingGenerator);

    public async Task<IngestionResult> IngestAsync(
        string id, string kind, string title, string content, CancellationToken cancellationToken)
    {
        var collection = await knowledge.GetAsync(cancellationToken);

        var embeddingModel = EmbeddingModel;
        var contentHash = Hash(kind, title, embeddingModel, content);
        var existing = await GetStoredChunksAsync(collection, id, cancellationToken);

        if (existing.Count > 0 && existing.All(c => c.ContentHash == contentHash))
        {
            return new IngestionResult(
                new KnowledgeDocumentResponse(id, kind, title, existing.Count, embeddingModel, IngestionStatus.Unchanged),
                Created: false);
        }

        var chunks = MarkdownChunker.Chunk(content);
        var embeddings = await KnowledgeEmbeddings.GenerateAsync(
            embeddingGenerator, chunks.Select(chunk => EmbeddingInput(title, chunk)).ToList(), cancellationToken);
        var ingestedAt = timeProvider.GetUtcNow();

        var records = chunks.Select((chunk, i) => new KnowledgeChunkRecord
        {
            Key = KnowledgeChunkRecord.KeyFor(id, chunk.Index),
            DocumentId = id,
            Kind = kind,
            Title = title,
            ChunkIndex = chunk.Index,
            Section = chunk.Section,
            Text = chunk.Text,
            ContentHash = contentHash,
            EmbeddingModel = embeddingModel,
            IngestedAt = ingestedAt,
            Embedding = embeddings[i],
        }).ToList();

        await collection.UpsertAsync(records, cancellationToken);

        var leftovers = existing.Where(c => c.ChunkIndex >= records.Count).Select(c => c.Key).ToList();
        if (leftovers.Count > 0)
        {
            await collection.DeleteAsync(leftovers, cancellationToken);
        }

        var status = existing.Count == 0 ? IngestionStatus.Created : IngestionStatus.Updated;
        return new IngestionResult(
            new KnowledgeDocumentResponse(id, kind, title, records.Count, embeddingModel, status),
            Created: existing.Count == 0);
    }

    public async Task<IReadOnlyList<KnowledgeChunkRecord>> GetChunksAsync(string id, CancellationToken cancellationToken)
    {
        var collection = await knowledge.GetAsync(cancellationToken);
        return await GetStoredChunksAsync(collection, id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var collection = await knowledge.GetAsync(cancellationToken);
        var existing = await GetStoredChunksAsync(collection, id, cancellationToken);
        if (existing.Count == 0)
        {
            return false;
        }

        await collection.DeleteAsync(existing.Select(c => c.Key).ToList(), cancellationToken);
        return true;
    }

    // The header tells the embedding where a chunk comes from; the stored text stays verbatim.
    internal static string EmbeddingInput(string title, TextChunk chunk) =>
        chunk.Section.Length == 0
            ? $"{title}\n\n{chunk.Text}"
            : $"{title} — {chunk.Section}\n\n{chunk.Text}";

    private static async Task<List<KnowledgeChunkRecord>> GetStoredChunksAsync(
        VectorStoreCollection<string, KnowledgeChunkRecord> collection, string id, CancellationToken cancellationToken)
    {
        var chunks = new List<KnowledgeChunkRecord>();
        await foreach (var chunk in collection.GetAsync(r => r.DocumentId == id, MaxChunksPerDocument, cancellationToken: cancellationToken))
        {
            chunks.Add(chunk);
        }

        return chunks.OrderBy(c => c.ChunkIndex).ToList();
    }

    private static string Hash(string kind, string title, string embeddingModel, string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{kind}\n{title}\n{embeddingModel}\n{content}")));
}
