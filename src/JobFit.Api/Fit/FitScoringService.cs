using JobFit.Api.Evidence;

namespace JobFit.Api.Fit;

public sealed class FitScoringService(EvidenceRetriever retriever, FitJudge judge)
{
    public const int EvidencePerRequirement = 3;
    public const int MaxConcurrentJudgments = 4;

    public async Task<ScoreFitResponse> ScoreAsync(
        IReadOnlyList<FitRequirement> requirements, string? kind, CancellationToken cancellationToken)
    {
        var evidence = await retriever.SearchAsync(
            requirements.Select(r => (r.Id, r.Description)).ToList(), kind, EvidencePerRequirement, cancellationToken);

        var assessments = new RequirementAssessment[requirements.Count];
        await Parallel.ForAsync(
            0,
            requirements.Count,
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentJudgments, CancellationToken = cancellationToken },
            async (i, token) => assessments[i] = await judge.JudgeAsync(requirements[i], evidence.Results[i].Evidence, token));

        return new ScoreFitResponse(
            FitScorer.Score(assessments),
            IdsWith(assessments, FitStatus.Met),
            IdsWith(assessments, FitStatus.Partial),
            IdsWith(assessments, FitStatus.Missing),
            assessments,
            judge.ChatModel,
            evidence.EmbeddingModel);
    }

    private static List<string> IdsWith(IEnumerable<RequirementAssessment> assessments, FitStatus status) =>
        assessments.Where(a => a.Status == status).Select(a => a.RequirementId).ToList();
}
