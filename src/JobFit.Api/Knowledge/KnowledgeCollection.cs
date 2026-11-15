using Microsoft.Extensions.VectorData;

namespace JobFit.Api.Knowledge;

// Creates the collection on first use rather than at startup, so the API starts without the database.
public sealed class KnowledgeCollection(
    VectorStoreCollection<string, KnowledgeChunkRecord> collection,
    ILogger<KnowledgeCollection> logger)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private volatile bool _ready;

    public async Task<VectorStoreCollection<string, KnowledgeChunkRecord>> GetAsync(CancellationToken cancellationToken)
    {
        if (_ready)
        {
            return collection;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_ready)
            {
                await collection.EnsureCollectionExistsAsync(cancellationToken);
                _ready = true;
                logger.LogInformation("Knowledge collection {Collection} is ready", KnowledgeChunkRecord.CollectionName);
            }
        }
        finally
        {
            _lock.Release();
        }

        return collection;
    }
}
