using JobFit.Api.Evidence;
using JobFit.Api.Knowledge;
using JobFit.Api.Requirements;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JobFit.Api.Fit;

public static class FitEndpoints
{
    public static IEndpointRouteBuilder MapFitEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/fit/score", ScoreAsync).WithName("ScoreFit");
        return app;
    }

    private static async Task<Results<Ok<ScoreFitResponse>, ValidationProblem, ProblemHttpResult>> ScoreAsync(
        ScoreFitRequest request,
        FitScoringService scoring,
        ILogger<FitScoringService> logger,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        EvidenceValidation.Validate(request.Requirements?.Select(r => (r?.Id, r?.Description)).ToList(), request.Kind, errors);
        for (var i = 0; i < (request.Requirements?.Count ?? 0); i++)
        {
            if (ParseImportance(request.Requirements![i]?.Importance) is null)
            {
                errors[$"requirements[{i}].importance"] = ["The importance must be 'must-have' or 'nice-to-have'."];
            }
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var requirements = request.Requirements!
            .Select(r => new FitRequirement(r!.Id!, r.Description!, ParseImportance(r.Importance)!.Value))
            .ToList();
        try
        {
            return TypedResults.Ok(await scoring.ScoreAsync(requirements, request.Kind, cancellationToken));
        }
        catch (EmbeddingFailedException exception)
        {
            logger.LogError(exception, "Embedding failed while scoring fit");
            return TypedResults.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "The embedding model could not embed the requirements.");
        }
        catch (JudgeFailedException exception)
        {
            logger.LogError(exception, "The judge model failed while scoring fit");
            return TypedResults.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "The chat model could not judge the requirements.");
        }
    }

    private static RequirementImportance? ParseImportance(string? importance) => importance switch
    {
        "must-have" => RequirementImportance.MustHave,
        "nice-to-have" => RequirementImportance.NiceToHave,
        _ => null,
    };
}
