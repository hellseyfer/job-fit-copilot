namespace JobFit.Api.Requirements;

public static class RequirementGrounding
{
    public static IReadOnlyList<JobRequirement> Ground(string jobPosting, IEnumerable<ExtractedRequirement?> extracted)
    {
        var posting = Normalize(jobPosting);

        return extracted
            .Where(requirement => requirement is not null
                && !string.IsNullOrWhiteSpace(requirement.Description)
                && !string.IsNullOrWhiteSpace(requirement.Excerpt)
                && posting.Contains(Normalize(requirement.Excerpt), StringComparison.Ordinal))
            .Select((requirement, index) => new JobRequirement(
                $"req-{index + 1}",
                requirement!.Description!.Trim(),
                requirement.Category,
                requirement.Importance,
                requirement.Excerpt!.Trim()))
            .ToList();
    }

    public static string Normalize(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
