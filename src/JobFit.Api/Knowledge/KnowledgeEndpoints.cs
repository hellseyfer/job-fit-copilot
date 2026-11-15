using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JobFit.Api.Knowledge;

public static partial class KnowledgeEndpoints
{
    public const int MaxTitleLength = 200;
    public const int MaxContentLength = 50_000;

    public static IEndpointRouteBuilder MapKnowledgeEndpoints(this IEndpointRouteBuilder app)
    {
        var documents = app.MapGroup("/api/knowledge/documents");
        documents.MapPut("/{id}", PutAsync).WithName("PutKnowledgeDocument");
        documents.MapGet("/{id}/chunks", GetChunksAsync).WithName("GetKnowledgeDocumentChunks");
        documents.MapDelete("/{id}", DeleteAsync).WithName("DeleteKnowledgeDocument");
        return app;
    }

    private static async Task<Results<Created<KnowledgeDocumentResponse>, Ok<KnowledgeDocumentResponse>, ValidationProblem, ProblemHttpResult>> PutAsync(
        string id,
        PutKnowledgeDocumentRequest request,
        KnowledgeIngestionService ingestion,
        ILogger<KnowledgeIngestionService> logger,
        CancellationToken cancellationToken)
    {
        var errors = Validate(id, request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        try
        {
            var result = await ingestion.IngestAsync(id, request.Kind!, request.Title!, request.Content!, cancellationToken);
            return result.Created
                ? TypedResults.Created($"/api/knowledge/documents/{id}/chunks", result.Document)
                : TypedResults.Ok(result.Document);
        }
        catch (EmbeddingFailedException exception)
        {
            logger.LogError(exception, "Embedding failed while ingesting knowledge document {DocumentId}", id);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "The embedding model could not embed the document.");
        }
    }

    private static async Task<Results<Ok<KnowledgeChunksResponse>, NotFound>> GetChunksAsync(
        string id, KnowledgeIngestionService ingestion, CancellationToken cancellationToken)
    {
        var chunks = await ingestion.GetChunksAsync(id, cancellationToken);
        return chunks.Count == 0
            ? TypedResults.NotFound()
            : TypedResults.Ok(new KnowledgeChunksResponse(
                id, chunks.Select(c => new KnowledgeChunkResponse(c.ChunkIndex, c.Section, c.Text)).ToList()));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        string id, KnowledgeIngestionService ingestion, CancellationToken cancellationToken) =>
        await ingestion.DeleteAsync(id, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();

    private static Dictionary<string, string[]> Validate(string id, PutKnowledgeDocumentRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (!IdPattern().IsMatch(id))
        {
            errors["id"] = ["The id must be 1-64 lowercase letters, digits or hyphens, starting with a letter or digit."];
        }

        if (!KnowledgeDocumentKinds.IsValid(request.Kind))
        {
            errors["kind"] = [$"The kind must be '{KnowledgeDocumentKinds.Cv}' or '{KnowledgeDocumentKinds.Project}'."];
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > MaxTitleLength)
        {
            errors["title"] = [$"The title is required and must be at most {MaxTitleLength} characters long."];
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            errors["content"] = ["The content is required."];
        }
        else if (request.Content.Length > MaxContentLength)
        {
            errors["content"] = [$"The content must be at most {MaxContentLength} characters long."];
        }

        return errors;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex IdPattern();
}
