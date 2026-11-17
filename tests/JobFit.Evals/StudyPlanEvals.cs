using System.Text.Json;
using CommunityToolkit.VectorData.InMemory;
using JobFit.Api.Analysis;
using JobFit.Api.Evidence;
using JobFit.Api.Fit;
using JobFit.Api.Knowledge;
using JobFit.Api.Requirements;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace JobFit.Evals;

// Measures how many gap and partial requirements get a study item, with real chat and embedding models.
// Skipped unless OPENAI_API_KEY or OLLAMA_ENDPOINT is set (or fails when JOBFIT_EVAL_REQUIRED=true).
[Collection("Evals")]
public class StudyPlanEvals(ITestOutputHelper output)
{
    private const double MinimumCoverage = 0.8;

    [SkippableFact]
    public async Task Study_plan_covers_the_gaps_of_a_weak_fit()
    {
        using var generator = EvalHarness.RequireEmbeddingGenerator();
        using var chatClient = EvalHarness.RequireChatClient();

        var cv = Load<GoldenCv>("evidence-retrieval.json");
        var golden = Load<GoldenPosting>("study-plan.json");
        using var store = new InMemoryCollection<string, KnowledgeChunkRecord>(KnowledgeChunkRecord.CollectionName);
        var knowledge = new KnowledgeCollection(store, NullLogger<KnowledgeCollection>.Instance);
        await new KnowledgeIngestionService(knowledge, generator, TimeProvider.System)
            .IngestAsync("golden-cv", KnowledgeDocumentKinds.Cv, cv.Title, cv.Cv, CancellationToken.None);
        var scoring = new FitScoringService(new EvidenceRetriever(knowledge, generator), new FitJudge(chatClient));

        var fit = await scoring.ScoreAsync(
            golden.Requirements.Select(r => new FitRequirement(
                r.Id, r.Description, r.Importance == "must-have" ? RequirementImportance.MustHave : RequirementImportance.NiceToHave)).ToList(),
            kind: null,
            CancellationToken.None);
        var plan = await new StudyPlanner(chatClient).PlanAsync(fit.Assessments, CancellationToken.None);

        var gaps = fit.Assessments.Where(a => a.Status != FitStatus.Met).ToList();
        var planned = plan.Select(i => i.RequirementId).ToHashSet();
        foreach (var gap in gaps)
        {
            var item = plan.FirstOrDefault(i => i.RequirementId == gap.RequirementId);
            output.WriteLine($"{(item is null ? "FAIL" : "OK  ")} {gap.Status,-8} {gap.Description}");
            if (item is not null)
            {
                output.WriteLine($"       {item.Focus} | {string.Join(" / ", item.Steps)}");
            }
        }

        Assert.True(gaps.Count > 0, "The golden posting should leave gaps.");
        var coverage = (double)gaps.Count(g => planned.Contains(g.RequirementId)) / gaps.Count;
        output.WriteLine($"chat={fit.ChatModel} score={fit.Score} gaps={gaps.Count} coverage={coverage:F2}");

        var passed = coverage >= MinimumCoverage;
        EvalReport.Record("study-plan", "gap coverage", fit.ChatModel, coverage, $">= {MinimumCoverage:F2}", passed);
        Assert.True(passed, $"coverage {coverage:F2} is below {MinimumCoverage}");
    }

    private static T Load<T>(string file) => JsonSerializer.Deserialize<T>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", file)), JsonSerializerOptions.Web)!;

    private sealed record GoldenCv(string Title, string Cv);

    private sealed record GoldenPosting(IReadOnlyList<GoldenRequirement> Requirements);

    private sealed record GoldenRequirement(string Id, string Description, string Importance);
}
