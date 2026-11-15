using JobFit.Api.Knowledge;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JobFit.Api.Evidence;

public static class EvidenceEndpoints
{
    public const int DefaultTop = 3;
    public const int MaxTop = 10;

    public static IEndpointRouteBuilder MapEvidenceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/evidence/search", SearchAsync).WithName("SearchEvidence");
        return app;
    }

    private static async Task<Results<Ok<SearchEvidenceResponse>, ValidationProblem, ProblemHttpResult>> SearchAsync(
        SearchEvidenceRequest request,
        EvidenceRetriever retriever,
        ILogger<EvidenceRetriever> logger,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        try
        {
            var requirements = request.Requirements!.Select(r => (r!.Id!, r.Description!)).ToList();
            return TypedResults.Ok(await retriever.SearchAsync(
                requirements, request.Kind, request.Top ?? DefaultTop, cancellationToken));
        }
        catch (EmbeddingFailedException exception)
        {
            logger.LogError(exception, "Embedding failed while searching evidence");
            return TypedResults.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "The embedding model could not embed the requirements.");
        }
    }

    private static Dictionary<string, string[]> Validate(SearchEvidenceRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        EvidenceValidation.Validate(
            request.Requirements?.Select(r => (r?.Id, r?.Description)).ToList(), request.Kind, errors);

        if (request.Top is < 1 or > MaxTop)
        {
            errors["top"] = [$"The top must be between 1 and {MaxTop}."];
        }

        return errors;
    }
}
