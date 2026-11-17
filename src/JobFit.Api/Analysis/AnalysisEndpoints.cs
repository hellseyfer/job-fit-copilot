using JobFit.Api.CoverLetters;
using JobFit.Api.Evidence;
using JobFit.Api.Fit;
using JobFit.Api.Guardrails;
using JobFit.Api.Knowledge;
using JobFit.Api.Observability;
using JobFit.Api.Requirements;
using Microsoft.AspNetCore.Http.HttpResults;

namespace JobFit.Api.Analysis;

public static class AnalysisEndpoints
{
    public const int MaxFeedbackLength = 2_000;

    public static IEndpointRouteBuilder MapAnalysisEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/analyses", AnalyzeAsync).WithName("AnalyzeJob");
        app.MapGet("/api/analyses/{runId}", GetAsync).WithName("GetAnalysis");
        app.MapPost("/api/analyses/{runId}/approval", DecideAsync).WithName("DecideAnalysis");
        return app;
    }

    private static async Task<Results<Ok<AnalyzeJobResponse>, Accepted<AnalyzeJobResponse>, ValidationProblem, ProblemHttpResult>> AnalyzeAsync(
        AnalyzeJobRequest request,
        JobAnalysisWorkflow workflow,
        InputGuard guard,
        UsageTracker usageTracker,
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
            using var usageScope = usageTracker.BeginScope();
            await guard.ScreenAsync("jobPosting", request.JobPosting, cancellationToken);
            return Respond(await workflow.StartAsync(request.JobPosting!, request.Kind, cancellationToken));
        }
        catch (PromptInjectionException exception)
        {
            return InputGuard.Problem(exception);
        }
        catch (GuardrailFailedException exception)
        {
            logger.LogError(exception, "The guardrail classifier failed while screening a job posting");
            return InputGuard.Problem(exception);
        }
        catch (NoScorableRequirementsException exception)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: exception.Message);
        }
        catch (Exception exception) when (IsModelFailure(exception))
        {
            logger.LogError(exception, "A model call failed while analyzing a job posting");
            return TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: exception.Message);
        }
    }

    private static async Task<Results<Ok<AnalyzeJobResponse>, ProblemHttpResult>> GetAsync(
        string runId, JobAnalysisWorkflow workflow, CancellationToken cancellationToken) =>
        await workflow.GetAsync(runId, cancellationToken) is { } run
            ? TypedResults.Ok(run)
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: new AnalysisRunNotFoundException(runId).Message);

    private static async Task<Results<Ok<AnalyzeJobResponse>, Accepted<AnalyzeJobResponse>, ValidationProblem, ProblemHttpResult>> DecideAsync(
        string runId,
        ApprovalRequest request,
        JobAnalysisWorkflow workflow,
        InputGuard guard,
        UsageTracker usageTracker,
        ILogger<JobAnalysisWorkflow> logger,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Approved is null)
        {
            errors["approved"] = ["The decision is required: true to approve the cover letter, false to reject it."];
        }

        if (request.Feedback?.Length > MaxFeedbackLength)
        {
            errors["feedback"] = [$"The feedback must be at most {MaxFeedbackLength} characters long."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        try
        {
            using var usageScope = usageTracker.BeginScope();
            // Screened before the run is claimed, so a blocked feedback leaves it awaiting approval.
            await guard.ScreenAsync("feedback", request.Feedback, cancellationToken);
            return Respond(await workflow.DecideAsync(runId, request.Approved!.Value, request.Feedback, cancellationToken));
        }
        catch (PromptInjectionException exception)
        {
            return InputGuard.Problem(exception);
        }
        catch (GuardrailFailedException exception)
        {
            logger.LogError(exception, "The guardrail classifier failed while screening feedback for run {RunId}", runId);
            return InputGuard.Problem(exception);
        }
        catch (AnalysisRunNotFoundException exception)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: exception.Message);
        }
        catch (AnalysisRunConflictException exception)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: exception.Message);
        }
        catch (Exception exception) when (IsModelFailure(exception))
        {
            logger.LogError(exception, "A model call failed while resuming analysis run {RunId}", runId);
            return TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: exception.Message);
        }
    }

    private static Results<Ok<AnalyzeJobResponse>, Accepted<AnalyzeJobResponse>, ValidationProblem, ProblemHttpResult> Respond(
        AnalyzeJobResponse response) =>
        response.Status == AnalysisStatus.AwaitingApproval
            ? TypedResults.Accepted($"/api/analyses/{response.RunId}", response)
            : TypedResults.Ok(response);

    private static bool IsModelFailure(Exception exception) =>
        exception is ExtractionFailedException or EmbeddingFailedException or JudgeFailedException
            or StudyPlanFailedException or CoverLetterFailedException;
}
