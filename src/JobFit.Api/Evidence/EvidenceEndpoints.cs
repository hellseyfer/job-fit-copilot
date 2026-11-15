using JobFit.Api.Knowledge;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JobFit.Api.Evidence;

public static class EvidenceEndpoints
{
    public const int MaxRequirements = 50;
    public const int MaxDescriptionLength = 1_000;
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

        if (request.Requirements is not { Count: > 0 and <= MaxRequirements } requirements)
        {
            errors["requirements"] = [$"Between 1 and {MaxRequirements} requirements are required."];
        }
        else
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < requirements.Count; i++)
            {
                var requirement = requirements[i];
                if (string.IsNullOrWhiteSpace(requirement?.Id))
                {
                    errors[$"requirements[{i}].id"] = ["The id is required."];
                }
                else if (!ids.Add(requirement.Id))
                {
                    errors[$"requirements[{i}].id"] = [$"The id '{requirement.Id}' is repeated."];
                }

                if (string.IsNullOrWhiteSpace(requirement?.Description) || requirement.Description.Length > MaxDescriptionLength)
                {
                    errors[$"requirements[{i}].description"] =
                        [$"The description is required and must be at most {MaxDescriptionLength} characters long."];
                }
            }
        }

        if (request.Top is < 1 or > MaxTop)
        {
            errors["top"] = [$"The top must be between 1 and {MaxTop}."];
        }

        if (request.Kind is not null && !KnowledgeDocumentKinds.IsValid(request.Kind))
        {
            errors["kind"] = [$"The kind must be '{KnowledgeDocumentKinds.Cv}' or '{KnowledgeDocumentKinds.Project}'."];
        }

        return errors;
    }
}
