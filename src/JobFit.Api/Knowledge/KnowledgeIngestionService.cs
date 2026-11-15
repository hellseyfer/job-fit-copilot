using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

namespace JobFit.Api.Knowledge;

public sealed class EmbeddingFailedException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed record IngestionResult(KnowledgeDocumentResponse Document, bool Created);

public sealed class KnowledgeIngestionService(
    VectorStoreCollection<string, KnowledgeChunkRecord> collection,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    TimeProvider timeProvider,
    ILogger<KnowledgeIngestionService> logger)
{
    // Every chunk holds at least one character of the content, so this bounds the chunks of any valid document.
    private const int MaxChunksPerDocument = KnowledgeEndpoints.MaxContentLength;

    private readonly SemaphoreSlim _collectionLock = new(1, 1);
    private volatile bool _collectionReady;

    private string EmbeddingModel =>
        embeddingGenerator.GetService<EmbeddingGeneratorMetadata>()?.DefaultModelId ?? "unknown";

    public async Task<IngestionResult> IngestAsync(
        string id, string kind, string title, string content, CancellationToken cancellationToken)
    {
        await EnsureCollectionAsync(cancellationToken);

        var embeddingModel = EmbeddingModel;
        var contentHash = Hash(kind, title, embeddingModel, content);
        var existing = await GetStoredChunksAsync(id, cancellationToken);

        if (existing.Count > 0 && existing.All(c => c.ContentHash == contentHash))
        {
            return new IngestionResult(
                new KnowledgeDocumentResponse(id, kind, title, existing.Count, embeddingModel, IngestionStatus.Unchanged),
                Created: false);
        }

        var chunks = MarkdownChunker.Chunk(content);
        var embeddings = await EmbedAsync(title, chunks, cancellationToken);
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
        await EnsureCollectionAsync(cancellationToken);
        return await GetStoredChunksAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        await EnsureCollectionAsync(cancellationToken);
        var existing = await GetStoredChunksAsync(id, cancellationToken);
        if (existing.Count == 0)
        {
            return false;
        }

        await collection.DeleteAsync(existing.Select(c => c.Key).ToList(), cancellationToken);
        return true;
    }

    private async Task<List<ReadOnlyMemory<float>>> EmbedAsync(
        string title, IReadOnlyList<TextChunk> chunks, CancellationToken cancellationToken)
    {
        GeneratedEmbeddings<Embedding<float>> generated;
        try
        {
            generated = await embeddingGenerator.GenerateAsync(
                chunks.Select(chunk => EmbeddingInput(title, chunk)), cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EmbeddingFailedException("The embedding model call failed.", exception);
        }

        if (generated.Count != chunks.Count)
        {
            throw new EmbeddingFailedException(
                $"The embedding model returned {generated.Count} vectors for {chunks.Count} chunks.");
        }

        var mismatch = generated.FirstOrDefault(e => e.Vector.Length != KnowledgeChunkRecord.Dimensions);
        if (mismatch is not null)
        {
            throw new EmbeddingFailedException(
                $"The embedding model returned {mismatch.Vector.Length} dimensions; the store expects {KnowledgeChunkRecord.Dimensions}.");
        }

        return generated.Select(e => e.Vector).ToList();
    }

    // The header tells the embedding where a chunk comes from; the stored text stays verbatim.
    internal static string EmbeddingInput(string title, TextChunk chunk) =>
        chunk.Section.Length == 0
            ? $"{title}\n\n{chunk.Text}"
            : $"{title} — {chunk.Section}\n\n{chunk.Text}";

    private async Task<List<KnowledgeChunkRecord>> GetStoredChunksAsync(string id, CancellationToken cancellationToken)
    {
        var chunks = new List<KnowledgeChunkRecord>();
        await foreach (var chunk in collection.GetAsync(r => r.DocumentId == id, MaxChunksPerDocument, cancellationToken: cancellationToken))
        {
            chunks.Add(chunk);
        }

        return chunks.OrderBy(c => c.ChunkIndex).ToList();
    }

    // Created on first use rather than at startup, so the API starts without the database.
    private async Task EnsureCollectionAsync(CancellationToken cancellationToken)
    {
        if (_collectionReady)
        {
            return;
        }

        await _collectionLock.WaitAsync(cancellationToken);
        try
        {
            if (!_collectionReady)
            {
                await collection.EnsureCollectionExistsAsync(cancellationToken);
                _collectionReady = true;
                logger.LogInformation("Knowledge collection {Collection} is ready", KnowledgeChunkRecord.CollectionName);
            }
        }
        finally
        {
            _collectionLock.Release();
        }
    }

    private static string Hash(string kind, string title, string embeddingModel, string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{kind}\n{title}\n{embeddingModel}\n{content}")));
}
