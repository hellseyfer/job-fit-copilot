namespace JobFit.Api.Knowledge;

public sealed record PutKnowledgeDocumentRequest(string? Kind, string? Title, string? Content);

public sealed record KnowledgeDocumentResponse(
    string Id, string Kind, string Title, int ChunkCount, string EmbeddingModel, string Status);

public sealed record KnowledgeChunksResponse(string DocumentId, IReadOnlyList<KnowledgeChunkResponse> Chunks);

public sealed record KnowledgeChunkResponse(int Index, string Section, string Text);

public static class KnowledgeDocumentKinds
{
    public const string Cv = "cv";
    public const string Project = "project";

    public static bool IsValid(string? kind) => kind is Cv or Project;
}

public static class IngestionStatus
{
    public const string Created = "created";
    public const string Updated = "updated";
    public const string Unchanged = "unchanged";
}
