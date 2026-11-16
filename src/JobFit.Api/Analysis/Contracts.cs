using System.Text.Json.Serialization;
using JobFit.Api.CoverLetters;
using JobFit.Api.Fit;
using JobFit.Api.Requirements;

namespace JobFit.Api.Analysis;

public sealed record AnalyzeJobRequest(string? JobPosting, string? Kind);

public enum AnalysisRoute { StrongFit, WeakFit }

// Resuming is internal: a decision has claimed the run and the workflow is running again.
public enum AnalysisStatus { AwaitingApproval, Approved, Rejected, Completed, Resuming }

public sealed record ApprovalRequest(bool? Approved, string? Feedback);

public sealed record StudyItem(string RequirementId, string Focus, IReadOnlyList<string> Steps);

public sealed record AnalyzeJobResponse(
    string RunId,
    AnalysisStatus Status,
    string Title,
    Seniority Seniority,
    IReadOnlyList<JobRequirement> Requirements,
    ScoreFitResponse Fit,
    AnalysisRoute Route,
    IReadOnlyList<string> Path,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CoverLetter? CoverLetter,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<StudyItem>? StudyPlan);

public sealed class WorkflowOptions
{
    public int FitThreshold { get; set; } = 70;
}

// Messages that flow along the graph edges; each stage carries everything the next nodes need.
public sealed record AnalysisInput(string JobPosting, string? Kind);

public sealed record ExtractedStage(ExtractRequirementsResponse Posting, string? Kind);

public sealed record FitStage(ExtractRequirementsResponse Posting, ScoreFitResponse Fit);

public sealed record AnalysisOutcome(
    FitStage Stage,
    AnalysisRoute Route,
    IReadOnlyList<StudyItem>? StudyPlan,
    CoverLetter? CoverLetter = null,
    AnalysisStatus Status = AnalysisStatus.Completed);

// Sent to the review-cover-letter port: the outcome so far and the verified draft it was finalized from.
public sealed record CoverLetterReview(AnalysisOutcome Outcome, LetterDraft Draft);

// The port's response carries the review back, so apply-review keeps no state of its own.
public sealed record ReviewDecision(CoverLetterReview Review, bool Approved, string? Feedback);

public sealed record LetterRevision(LetterDraft Reviewed, string Feedback);

public sealed class ExtractionFailedException(Exception innerException)
    : Exception("The chat model could not extract the requirements.", innerException);

public sealed class NoScorableRequirementsException(int count)
    : Exception($"The job posting yielded {count} requirements; between 1 and 50 can be scored.");

public sealed class AnalysisRunNotFoundException(string runId) : Exception($"The analysis run '{runId}' does not exist.");

public sealed class AnalysisRunConflictException(string runId)
    : Exception($"The analysis run '{runId}' is not awaiting approval.");
