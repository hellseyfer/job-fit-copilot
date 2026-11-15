using JobFit.Api.Evidence;
using JobFit.Api.Requirements;

namespace JobFit.Api.Fit;

// Accepts the requirements returned by POST /api/requirements/extract; extra fields are ignored.
public sealed record ScoreFitRequest(IReadOnlyList<FitRequirementInput?>? Requirements, string? Kind);

public sealed record FitRequirementInput(string? Id, string? Description, string? Importance);

public sealed record FitRequirement(string Id, string Description, RequirementImportance Importance);

public enum FitStatus { Met, Partial, Missing }

public sealed record RequirementAssessment(
    string RequirementId,
    string Description,
    RequirementImportance Importance,
    FitStatus Status,
    string Rationale,
    IReadOnlyList<EvidenceItem> Evidence);

public sealed record ScoreFitResponse(
    int Score,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Partial,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<RequirementAssessment> Assessments,
    string ChatModel,
    string EmbeddingModel);
