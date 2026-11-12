namespace JobFit.Api.Requirements;

public sealed record ExtractRequirementsRequest(string? JobPosting);

public sealed record ExtractRequirementsResponse(
    string Title,
    Seniority Seniority,
    IReadOnlyList<JobRequirement> Requirements);

public sealed record JobRequirement(
    string Id,
    string Description,
    RequirementCategory Category,
    RequirementImportance Importance,
    string Excerpt);

public enum Seniority { Junior, Mid, Senior, Lead, Unspecified }

public enum RequirementCategory { Skill, Experience, Education, Language, Certification, Other }

public enum RequirementImportance { MustHave, NiceToHave }
