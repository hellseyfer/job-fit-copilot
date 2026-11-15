using JobFit.Api.CoverLetters;
using JobFit.Api.Evidence;
using JobFit.Api.Fit;
using JobFit.Api.Knowledge;
using JobFit.Api.Requirements;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JobFit.Api.Analysis;

public static class AnalysisEndpoints
{
    public static IEndpointRouteBuilder MapAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/analyses", AnalyzeAsync).WithName("AnalyzeJob");
        return app;
    }

    private static async Task<Results<Ok<AnalyzeJobResponse>, ValidationProblem, ProblemHttpResult>> AnalyzeAsync(
        AnalyzeJobRequest request,
        JobAnalysisWorkflow workflow,
        ILogger<JobAnalysisWorkflow> logger,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.JobPosting) || request.JobPosting.Length > RequirementEndpoints.MaxJobPostingLength)
        {
            errors["jobPosting"] =
                [$"The job posting is required and must be at most {RequirementEndpoints.MaxJobPostingLength} characters long."];
        }

        if (request.Kind is not null && !KnowledgeDocumentKinds.IsValid(request.Kind))
        {
            errors["kind"] = [$"The kind must be '{KnowledgeDocumentKinds.Cv}' or '{KnowledgeDocumentKinds.Project}'."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        try
        {
            return TypedResults.Ok(await workflow.RunAsync(request.JobPosting!, request.Kind, cancellationToken));
        }
        catch (NoScorableRequirementsException exception)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: exception.Message);
        }
        catch (Exception exception) when (exception is ExtractionFailedException or EmbeddingFailedException
                                              or JudgeFailedException or StudyPlanFailedException
                                              or CoverLetterFailedException)
        {
            logger.LogError(exception, "A model call failed while analyzing a job posting");
            return TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: exception.Message);
        }
    }
}
