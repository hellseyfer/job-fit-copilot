using System.Runtime.ExceptionServices;
using JobFit.Api.CoverLetters;
using JobFit.Api.Evidence;
using JobFit.Api.Fit;
using JobFit.Api.Observability;
using JobFit.Api.Requirements;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.Options;

namespace JobFit.Api.Analysis;

public sealed class JobAnalysisWorkflow(
    RequirementExtractor extractor,
    FitScoringService scoring,
    StudyPlanner planner,
    CoverLetterWriter writer,
    CoverLetterVerifier verifier,
    PostgresCheckpointStore checkpoints,
    AnalysisRunStore runs,
    UsageTracker usageTracker,
    IOptions<WorkflowOptions> options)
{
    public const string ExtractRequirements = "extract-requirements";
    public const string ScoreFit = "score-fit";
    public const string WriteCoverLetter = "write-cover-letter";
    public const string VerifyCoverLetter = "verify-cover-letter";
    public const string FinalizeCoverLetter = "finalize-cover-letter";
    public const string ReviewCoverLetter = "review-cover-letter";
    public const string ApplyReview = "apply-review";
    public const string PlanStudy = "plan-study";
    public const int MaxFeedbackRounds = 3;

    //  extract-requirements ──► score-fit ──switch──► write-cover-letter ──► verify-cover-letter ──switch──► write-cover-letter    (rewrite due)
    //                                         │      (score ≥ threshold)                              └──► finalize-cover-letter (otherwise)
    //                                         └────► plan-study (otherwise)
    //  finalize-cover-letter ──► review-cover-letter (request port: the run pauses) ──► apply-review ──► output (approved | rejected)
    //                                                                                             └──► write-cover-letter (rejected with feedback)
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

        var write = new WriteCoverLetterExecutor(writer);

        var verify = ExecutorBindingExtensions.BindAsExecutor<LetterDraft, LetterDraft>(
            async (draft, _, cancellationToken) => await verifier.VerifyAsync(draft, cancellationToken),
            VerifyCoverLetter);

        var finalize = ExecutorBindingExtensions.BindAsExecutor<LetterDraft, CoverLetterReview>(
            draft => new CoverLetterReview(
                new AnalysisOutcome(
                    draft.Stage, AnalysisRoute.StrongFit, null, CoverLetterGrounding.Finalize(draft), AnalysisStatus.AwaitingApproval),
                draft),
            FinalizeCoverLetter);

        var review = RequestPort.Create<CoverLetterReview, ReviewDecision>(ReviewCoverLetter);

        var apply = new ApplyReviewExecutor();

        var plan = ExecutorBindingExtensions.BindAsExecutor<FitStage, AnalysisOutcome>(
            async (stage, _, cancellationToken) => new AnalysisOutcome(
                stage, AnalysisRoute.WeakFit, await planner.PlanAsync(stage.Fit.Assessments, cancellationToken)),
            PlanStudy);

        return new WorkflowBuilder(extract)
            .WithName("job-analysis")
            .AddEdge(extract, score)
            .AddSwitch(score, route => route
                .AddCase<FitStage>(stage => IsStrongFit(stage!.Fit.Score, threshold), [write])
                .WithDefault([plan]))
            .AddEdge(write, verify)
            .AddSwitch(verify, loop => loop
                .AddCase<LetterDraft>(draft => draft!.RewriteDue, [write])
                .WithDefault([finalize]))
            .AddEdge(finalize, review)
            .AddEdge(review, apply)
            .AddEdge(apply, write)
            .WithOutputFrom(apply, plan)
            .WithOpenTelemetry()
            .Build();
    }

    public static bool IsStrongFit(int score, int threshold) => score >= threshold;

    // Runs the analysis; a strong fit pauses at the review port and is returned awaiting approval.
    public async Task<AnalyzeJobResponse> StartAsync(string jobPosting, string? kind, CancellationToken cancellationToken)
    {
        using var usageScope = usageTracker.BeginScopeIfNone();
        var input = new AnalysisInput(jobPosting, kind);
        await using var run = await InProcessExecution.RunAsync(
            Build(), input, CheckpointManager.CreateJson(checkpoints), cancellationToken: cancellationToken);

        var step = Collect(run.NewEvents, cancellationToken);
        var usage = usageScope.Scope.Snapshot();
        var response = ToResponse(run.SessionId, [], step, usage);
        await runs.CreateAsync(input, response, step.Checkpoint?.CheckpointId, step.FeedbackRounds, cancellationToken);
        UsageTracker.RecordRunCompleted(usage, response.Route.ToString(), response.Status.ToString());
        return response;
    }

    public Task<AnalyzeJobResponse?> GetAsync(string runId, CancellationToken cancellationToken) =>
        runs.GetAsync(runId, cancellationToken);

    // Claims the paused run, restores it from its checkpoint and answers the pending review with the decision.
    public async Task<AnalyzeJobResponse> DecideAsync(
        string runId, bool approved, string? feedback, CancellationToken cancellationToken)
    {
        var claimed = await runs.ClaimAsync(runId, cancellationToken);
        if (claimed is null)
        {
            throw await runs.GetAsync(runId, cancellationToken) is null
                ? new AnalysisRunNotFoundException(runId)
                : new AnalysisRunConflictException(runId);
        }

        try
        {
            using var usageScope = usageTracker.BeginScopeIfNone();
            await using var run = await InProcessExecution.ResumeAsync(
                Build(), new CheckpointInfo(runId, claimed.CheckpointId), CheckpointManager.CreateJson(checkpoints), cancellationToken);
            var request = run.NewEvents.OfType<RequestInfoEvent>().Select(e => e.Request).FirstOrDefault()
                          ?? throw new InvalidOperationException($"The paused run '{runId}' has no pending review.");
            var pending = request.Data.As<CoverLetterReview>()
                          ?? throw new InvalidOperationException($"The pending review of run '{runId}' cannot be read.");

            await run.ResumeAsync([request.CreateResponse(new ReviewDecision(pending, approved, feedback))], cancellationToken);
            var step = Collect(run.NewEvents, cancellationToken);
            var deltaUsage = usageScope.Scope.Snapshot();
            var totalUsage = RunUsage.Combine(claimed.Response.Usage, deltaUsage);
            var response = ToResponse(runId, claimed.Response.Path, step, totalUsage);
            if (!await runs.UpdateAsync(
                    claimed.ClaimId, response, step.Checkpoint?.CheckpointId ?? claimed.CheckpointId, step.FeedbackRounds, cancellationToken))
            {
                throw new AnalysisRunConflictException(runId);
            }

            UsageTracker.RecordRunCompleted(deltaUsage, response.Route.ToString(), response.Status.ToString());
            return response;
        }
        catch
        {
            await runs.ReleaseAsync(runId, claimed.ClaimId, CancellationToken.None);
            throw;
        }
    }

    private sealed record Step(List<string> Path, AnalysisOutcome Outcome, CheckpointInfo? Checkpoint, int FeedbackRounds);

    private static Step Collect(IEnumerable<WorkflowEvent> events, CancellationToken cancellationToken)
    {
        var path = new List<string>();
        AnalysisOutcome? outcome = null;
        CoverLetterReview? pending = null;
        CheckpointInfo? checkpoint = null;
        foreach (var workflowEvent in events)
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
                case RequestInfoEvent request when request.Request.Data.Is<CoverLetterReview>(out var review):
                    pending = review;
                    break;
                case SuperStepCompletedEvent { CompletionInfo.Checkpoint: { } saved }:
                    checkpoint = saved;
                    break;
                case WorkflowOutputEvent output when output.Is<AnalysisOutcome>(out var result):
                    outcome = result;
                    break;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (pending is not null)
        {
            // The port completes when it raises the request; it only counts as run once the decision arrives.
            if (path.Count > 0 && path[^1] == ReviewCoverLetter)
            {
                path.RemoveAt(path.Count - 1);
            }

            return new Step(path, pending.Outcome, checkpoint, pending.Draft.FeedbackRounds);
        }

        return outcome is null
            ? throw new InvalidOperationException("The job analysis workflow ended without an outcome.")
            : new Step(path, outcome, checkpoint, 0);
    }

    private static AnalyzeJobResponse ToResponse(string runId, IEnumerable<string> pathSoFar, Step step, RunUsage usage)
    {
        var outcome = step.Outcome;
        var posting = outcome.Stage.Posting;
        return new AnalyzeJobResponse(
            runId,
            outcome.Status,
            posting.Title,
            posting.Seniority,
            posting.Requirements,
            outcome.Stage.Fit,
            outcome.Route,
            [.. pathSoFar, .. step.Path],
            outcome.CoverLetter,
            outcome.StudyPlan,
            usage);
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

    // The writer takes the scored posting for the first draft and a verified draft for each rewrite.
    private sealed class WriteCoverLetterExecutor(CoverLetterWriter writer) : Executor(WriteCoverLetter)
    {
        protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) =>
            protocolBuilder.ConfigureRoutes(routes => routes
                .AddHandler<FitStage, LetterDraft>((stage, _, cancellationToken) => new ValueTask<LetterDraft>(writer.WriteAsync(stage, cancellationToken)))
                .AddHandler<LetterDraft, LetterDraft>((draft, _, cancellationToken) => new ValueTask<LetterDraft>(writer.RewriteAsync(draft, cancellationToken)))
                .AddHandler<LetterRevision, LetterDraft>((revision, _, cancellationToken) => new ValueTask<LetterDraft>(writer.ReviseAsync(revision, cancellationToken))));
    }

    // Ends the run with the reviewer's decision, or sends the letter back to the writer with the feedback.
    private sealed class ApplyReviewExecutor() : Executor(ApplyReview)
    {
        protected override ProtocolBuilder ConfigureProtocol(ProtocolBuilder protocolBuilder) =>
            protocolBuilder
                .SendsMessage<LetterRevision>()
                .YieldsOutput<AnalysisOutcome>()
                .ConfigureRoutes(routes => routes.AddHandler<ReviewDecision>(async (decision, context, cancellationToken) =>
                {
                    var review = decision.Review;
                    var feedback = decision.Feedback?.Trim();
                    if (!decision.Approved && !string.IsNullOrEmpty(feedback) && review.Draft.FeedbackRounds < MaxFeedbackRounds)
                    {
                        await context.SendMessageAsync(new LetterRevision(review.Draft, feedback), cancellationToken: cancellationToken);
                        return;
                    }

                    var status = decision.Approved ? AnalysisStatus.Approved : AnalysisStatus.Rejected;
                    await context.YieldOutputAsync(review.Outcome with { Status = status }, cancellationToken);
                }));
    }
}
