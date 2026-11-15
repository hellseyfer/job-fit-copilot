using System.Text.Json.Serialization;
using JobFit.Api.CoverLetters;
using JobFit.Api.Fit;
using JobFit.Api.Requirements;

namespace JobFit.Api.Analysis;

public sealed record AnalyzeJobRequest(string? JobPosting, string? Kind);

public enum AnalysisRoute { StrongFit, WeakFit }

public sealed record StudyItem(string RequirementId, string Focus, IReadOnlyList<string> Steps);

public sealed record AnalyzeJobResponse(
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
    FitStage Stage, AnalysisRoute Route, IReadOnlyList<StudyItem>? StudyPlan, CoverLetter? CoverLetter = null);

public sealed class ExtractionFailedException(Exception innerException)
    : Exception("The chat model could not extract the requirements.", innerException);

public sealed class NoScorableRequirementsException(int count)
    : Exception($"The job posting yielded {count} requirements; between 1 and 50 can be scored.");
