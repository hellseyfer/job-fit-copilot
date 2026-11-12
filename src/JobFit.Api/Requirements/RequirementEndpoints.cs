using Microsoft.AspNetCore.Http.HttpResults;

namespace JobFit.Api.Requirements;

public static class RequirementEndpoints
{
    public const int MaxJobPostingLength = 20_000;

    public static IEndpointRouteBuilder MapRequirementEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/requirements/extract", ExtractAsync)
            .WithName("ExtractRequirements");

        return app;
    }

    private static async Task<Results<Ok<ExtractRequirementsResponse>, ValidationProblem, ProblemHttpResult>> ExtractAsync(
        ExtractRequirementsRequest request,
        RequirementExtractor extractor,
        ILogger<RequirementExtractor> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.JobPosting))
        {
            return InvalidJobPosting("The job posting is required.");
        }

        if (request.JobPosting.Length > MaxJobPostingLength)
        {
            return InvalidJobPosting($"The job posting must be at most {MaxJobPostingLength} characters long.");
        }

        try
        {
            return TypedResults.Ok(await extractor.ExtractAsync(request.JobPosting, cancellationToken));
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Requirement extraction failed");
            return TypedResults.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "The language model could not extract the requirements.");
        }
    }

    private static ValidationProblem InvalidJobPosting(string error) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["jobPosting"] = [error] });
}
