using System.Text.Json;
using CommunityToolkit.VectorData.InMemory;
using JobFit.Api.Evidence;
using JobFit.Api.Fit;
using JobFit.Api.Knowledge;
using JobFit.Api.Requirements;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace JobFit.Evals;

// Measures how often the LLM judge agrees with labeled requirements, with real chat and embedding models.
// Skipped unless OPENAI_API_KEY or OLLAMA_ENDPOINT is set (or fails when JOBFIT_EVAL_REQUIRED=true).
[Collection("Evals")]
public class FitScoringEvals(ITestOutputHelper output)
{
    private const double MinimumAccuracy = 0.8;

    [SkippableFact]
    public async Task Judgment_accuracy_on_the_golden_cv_meets_the_target()
    {
        using var generator = EvalHarness.RequireEmbeddingGenerator();
        using var chatClient = EvalHarness.RequireChatClient();

        var cv = Load<GoldenCv>("evidence-retrieval.json");
        var golden = Load<GoldenFit>("fit-scoring.json");
        using var store = new InMemoryCollection<string, KnowledgeChunkRecord>(KnowledgeChunkRecord.CollectionName);
        var knowledge = new KnowledgeCollection(store, NullLogger<KnowledgeCollection>.Instance);
        await new KnowledgeIngestionService(knowledge, generator, TimeProvider.System)
            .IngestAsync("golden-cv", KnowledgeDocumentKinds.Cv, cv.Title, cv.Cv, CancellationToken.None);
        var scoring = new FitScoringService(new EvidenceRetriever(knowledge, generator), new FitJudge(chatClient));

        var response = await scoring.ScoreAsync(
            golden.Requirements.Select(r => new FitRequirement(r.Id, r.Description, RequirementImportance.MustHave)).ToList(),
            kind: null,
            CancellationToken.None);

        var correct = 0;
        foreach (var (requirement, assessment) in golden.Requirements.Zip(response.Assessments))
        {
            var agrees = requirement.Supported == assessment.Status is FitStatus.Met or FitStatus.Partial;
            correct += agrees ? 1 : 0;
            output.WriteLine($"{(agrees ? "OK  " : "FAIL")} {assessment.Status,-8} {requirement.Description}");
            output.WriteLine($"       {assessment.Rationale} [{string.Join(", ", assessment.Evidence.Select(e => e.Section))}]");
        }

        var accuracy = (double)correct / golden.Requirements.Count;
        output.WriteLine($"chat={response.ChatModel} embeddings={response.EmbeddingModel} accuracy={accuracy:F2} score={response.Score}");

        var passed = accuracy >= MinimumAccuracy;
        EvalReport.Record("fit-scoring", "judgment accuracy", response.ChatModel, accuracy, $">= {MinimumAccuracy:F2}", passed);
        Assert.True(passed, $"accuracy {accuracy:F2} is below {MinimumAccuracy}");
    }

    private static T Load<T>(string file) => JsonSerializer.Deserialize<T>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", file)), JsonSerializerOptions.Web)!;

    private sealed record GoldenCv(string Title, string Cv);

    private sealed record GoldenFit(IReadOnlyList<GoldenFitRequirement> Requirements);

    private sealed record GoldenFitRequirement(string Id, string Description, bool Supported);
}
