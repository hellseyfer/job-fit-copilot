namespace JobFit.Api.Evidence;

// Accepts the requirements returned by POST /api/requirements/extract; extra fields are ignored.
public sealed record SearchEvidenceRequest(IReadOnlyList<EvidenceQuery?>? Requirements, string? Kind, int? Top);

public sealed record EvidenceQuery(string? Id, string? Description);

public sealed record SearchEvidenceResponse(string EmbeddingModel, IReadOnlyList<RequirementEvidence> Results);

public sealed record RequirementEvidence(string RequirementId, IReadOnlyList<EvidenceItem> Evidence);

public sealed record EvidenceItem(
    string DocumentId, string Kind, string Title, string Section, int ChunkIndex, string Text, double Score);
