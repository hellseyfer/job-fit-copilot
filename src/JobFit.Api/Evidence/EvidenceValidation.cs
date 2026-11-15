using JobFit.Api.Knowledge;

namespace JobFit.Api.Evidence;

public static class EvidenceValidation
{
    public const int MaxRequirements = 50;
    public const int MaxDescriptionLength = 1_000;

    // Shared by every endpoint that retrieves evidence for a list of requirements.
    public static void Validate(
        IReadOnlyList<(string? Id, string? Description)>? requirements,
        string? kind,
        Dictionary<string, string[]> errors)
    {
        if (requirements is not { Count: > 0 and <= MaxRequirements })
        {
            errors["requirements"] = [$"Between 1 and {MaxRequirements} requirements are required."];
        }
        else
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < requirements.Count; i++)
            {
                var (id, description) = requirements[i];
                if (string.IsNullOrWhiteSpace(id))
                {
                    errors[$"requirements[{i}].id"] = ["The id is required."];
                }
                else if (!ids.Add(id))
                {
                    errors[$"requirements[{i}].id"] = [$"The id '{id}' is repeated."];
                }

                if (string.IsNullOrWhiteSpace(description) || description.Length > MaxDescriptionLength)
                {
                    errors[$"requirements[{i}].description"] =
                        [$"The description is required and must be at most {MaxDescriptionLength} characters long."];
                }
            }
        }

        if (kind is not null && !KnowledgeDocumentKinds.IsValid(kind))
        {
            errors["kind"] = [$"The kind must be '{KnowledgeDocumentKinds.Cv}' or '{KnowledgeDocumentKinds.Project}'."];
        }
    }
}
