using Microsoft.Extensions.VectorData;

namespace JobFit.Api.Knowledge;

// One row of the knowledge_chunks collection; the attributes define the pgvector table.
public sealed class KnowledgeChunkRecord
{
    public const string CollectionName = "knowledge_chunks";
    public const int Dimensions = 768;

    [VectorStoreKey(StorageName = "key")]
    public string Key { get; set; } = "";

    [VectorStoreData(StorageName = "document_id", IsIndexed = true)]
    public string DocumentId { get; set; } = "";

    [VectorStoreData(StorageName = "kind", IsIndexed = true)]
    public string Kind { get; set; } = "";

    [VectorStoreData(StorageName = "title")]
    public string Title { get; set; } = "";

    [VectorStoreData(StorageName = "chunk_index")]
    public int ChunkIndex { get; set; }

    [VectorStoreData(StorageName = "section")]
    public string Section { get; set; } = "";

    [VectorStoreData(StorageName = "text")]
    public string Text { get; set; } = "";

    [VectorStoreData(StorageName = "content_hash")]
    public string ContentHash { get; set; } = "";

    [VectorStoreData(StorageName = "embedding_model")]
    public string EmbeddingModel { get; set; } = "";

    [VectorStoreData(StorageName = "ingested_at")]
    public DateTimeOffset IngestedAt { get; set; }

    [VectorStoreVector(Dimensions, StorageName = "embedding", DistanceFunction = DistanceFunction.CosineDistance, IndexKind = IndexKind.Hnsw)]
    public ReadOnlyMemory<float> Embedding { get; set; }

    public static string KeyFor(string documentId, int chunkIndex) => $"{documentId}:{chunkIndex}";
}
