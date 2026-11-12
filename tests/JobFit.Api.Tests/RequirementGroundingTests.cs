using JobFit.Api.Requirements;

namespace JobFit.Api.Tests;

public class RequirementGroundingTests
{
    private const string Posting = "We need 5+ years of C#.\nExperience with   Docker is required.";

    [Fact]
    public void Ungrounded_requirement_is_removed_and_grounded_ones_are_kept()
    {
        var requirements = RequirementGrounding.Ground(Posting,
        [
            Requirement("C#", "5+ years of C#"),
            Requirement("Go", "3 years of Go"),
        ]);

        var requirement = Assert.Single(requirements);
        Assert.Equal("C#", requirement.Description);
    }

    [Fact]
    public void Excerpt_matching_except_case_and_whitespace_is_kept()
    {
        var requirements = RequirementGrounding.Ground(Posting, [Requirement("Docker", "EXPERIENCE WITH Docker\nis required")]);

        Assert.Single(requirements);
    }

    [Fact]
    public void Requirement_ids_are_unique()
    {
        var requirements = RequirementGrounding.Ground(Posting,
        [
            Requirement("C#", "5+ years of C#"),
            Requirement("Go", "3 years of Go"),
            Requirement("Docker", "Experience with Docker"),
        ]);

        Assert.Equal(["req-1", "req-2"], requirements.Select(r => r.Id));
    }

    [Fact]
    public void Requirement_without_excerpt_is_removed()
    {
        var requirements = RequirementGrounding.Ground(Posting, [Requirement("C#", " ")]);

        Assert.Empty(requirements);
    }

    private static ExtractedRequirement Requirement(string description, string excerpt) =>
        new(description, RequirementCategory.Skill, RequirementImportance.MustHave, excerpt);
}
