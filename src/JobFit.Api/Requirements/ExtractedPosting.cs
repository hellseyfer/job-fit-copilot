using System.ComponentModel;

namespace JobFit.Api.Requirements;

// Structured output requested from the model; its JSON schema is derived from these records.
public sealed record ExtractedPosting(
    [property: Description("Job title as written in the posting.")] string? Title,
    [property: Description("Seniority stated or clearly implied by the posting; unspecified if unknown.")] Seniority Seniority,
    [property: Description("Every requirement the posting asks of the candidate.")] IReadOnlyList<ExtractedRequirement>? Requirements);

public sealed record ExtractedRequirement(
    [property: Description("Short, self-contained description of the requirement.")] string? Description,
    RequirementCategory Category,
    [property: Description("nice-to-have when the posting marks it as optional, preferred, desirable or a plus; otherwise must-have.")] RequirementImportance Importance,
    [property: Description("Exact text copied from the posting that states this requirement.")] string? Excerpt);
