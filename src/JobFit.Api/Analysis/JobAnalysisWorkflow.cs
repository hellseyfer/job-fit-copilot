using System.Runtime.ExceptionServices;
using JobFit.Api.Evidence;
using JobFit.Api.Fit;
using JobFit.Api.Requirements;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Options;

namespace JobFit.Api.Analysis;

public sealed class JobAnalysisWorkflow(
    RequirementExtractor extractor,
    FitScoringService scoring,
    StudyPlanner planner,
    IOptions<WorkflowOptions> options)
{
    public const string ExtractRequirements = "extract-requirements";
    public const string ScoreFit = "score-fit";
    public const string StrongFit = "strong-fit";
    public const string PlanStudy = "plan-study";

    //  extract-requirements ──► score-fit ──switch──► strong-fit  (score ≥ threshold)
    //                                         └────► plan-study  (otherwise)
    public Workflow Build()
    {
        var threshold = options.Value.FitThreshold;

        var extract = ExecutorBindingExtensions.BindAsExecutor<AnalysisInput, ExtractedStage>(
            async (input, _, cancellationToken) =>
            {
                ExtractRequirementsResponse posting;
                try
                {
                    posting = await extractor.ExtractAsync(input.JobPosting, cancellationToken);
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new ExtractionFailedException(exception);
                }

                if (posting.Requirements.Count is 0 or > EvidenceValidation.MaxRequirements)
                {
                    throw new NoScorableRequirementsException(posting.Requirements.Count);
                }

                return new ExtractedStage(posting, input.Kind);
            },
            ExtractRequirements);

        var score = ExecutorBindingExtensions.BindAsExecutor<ExtractedStage, FitStage>(
            async (stage, _, cancellationToken) =>
            {
                var requirements = stage.Posting.Requirements
                    .Select(r => new FitRequirement(r.Id, r.Description, r.Importance))
                    .ToList();
                return new FitStage(stage.Posting, await scoring.ScoreAsync(requirements, stage.Kind, cancellationToken));
            },
            ScoreFit);

        var strong = ExecutorBindingExtensions.BindAsExecutor<FitStage, AnalysisOutcome>(
            stage => new AnalysisOutcome(stage, AnalysisRoute.StrongFit, null),
            StrongFit);

        var plan = ExecutorBindingExtensions.BindAsExecutor<FitStage, AnalysisOutcome>(
            async (stage, _, cancellationToken) => new AnalysisOutcome(
                stage, AnalysisRoute.WeakFit, await planner.PlanAsync(stage.Fit.Assessments, cancellationToken)),
            PlanStudy);

        return new WorkflowBuilder(extract)
            .WithName("job-analysis")
            .AddEdge(extract, score)
            .AddSwitch(score, route => route
                .AddCase<FitStage>(stage => IsStrongFit(stage!.Fit.Score, threshold), [strong])
                .WithDefault([plan]))
            .WithOutputFrom(strong, plan)
            .WithOpenTelemetry()
            .Build();
    }

    public static bool IsStrongFit(int score, int threshold) => score >= threshold;

    public async Task<AnalyzeJobResponse> RunAsync(string jobPosting, string? kind, CancellationToken cancellationToken)
    {
        await using var run = await InProcessExecution.RunAsync(
            Build(), new AnalysisInput(jobPosting, kind), cancellationToken: cancellationToken);

        var path = new List<string>();
        AnalysisOutcome? outcome = null;
        foreach (var workflowEvent in run.NewEvents)
        {
            switch (workflowEvent)
            {
                case ExecutorFailedEvent failed:
                    Rethrow(failed.Data);
                    break;
                case WorkflowErrorEvent error:
                    Rethrow(error.Exception);
                    break;
                case ExecutorCompletedEvent completed:
                    path.Add(completed.ExecutorId);
                    break;
                case WorkflowOutputEvent output when output.Is<AnalysisOutcome>(out var result):
                    outcome = result;
                    break;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (outcome is null)
        {
            throw new InvalidOperationException("The job analysis workflow ended without an outcome.");
        }

        var posting = outcome.Stage.Posting;
        return new AnalyzeJobResponse(
            posting.Title, posting.Seniority, posting.Requirements, outcome.Stage.Fit, outcome.Route, path, outcome.StudyPlan);
    }

    // Executors fail inside the workflow runtime; surface the original exception so the endpoint can map it.
    private static void Rethrow(Exception? exception)
    {
        while (exception is AggregateException or System.Reflection.TargetInvocationException
               && exception.InnerException is not null)
        {
            exception = exception.InnerException;
        }

        ExceptionDispatchInfo.Throw(exception ?? new InvalidOperationException("The job analysis workflow failed."));
    }
}
