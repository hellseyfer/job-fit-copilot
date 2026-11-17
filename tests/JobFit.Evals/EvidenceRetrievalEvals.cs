using System.Text.Json;
using CommunityToolkit.VectorData.InMemory;
using JobFit.Api.Evidence;
using JobFit.Api.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace JobFit.Evals;

// Measures recall@3 of evidence retrieval with the real embedding model on an in-memory vector store.
// Skipped unless OPENAI_API_KEY or OLLAMA_ENDPOINT is set (or fails when JOBFIT_EVAL_REQUIRED=true).
[Collection("Evals")]
public class EvidenceRetrievalEvals(ITestOutputHelper output)
{
    private const int Top = 3;
    private const double MinimumRecall = 0.8;

    [SkippableFact]
    public async Task Recall_at_3_on_the_golden_cv_meets_the_target()
    {
        using var generator = EvalHarness.RequireEmbeddingGenerator();

        var golden = JsonSerializer.Deserialize<GoldenCv>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "evidence-retrieval.json")),
            JsonSerializerOptions.Web)!;
        using var store = new InMemoryCollection<string, KnowledgeChunkRecord>(KnowledgeChunkRecord.CollectionName);
        var knowledge = new KnowledgeCollection(store, NullLogger<KnowledgeCollection>.Instance);
        await new KnowledgeIngestionService(knowledge, generator, TimeProvider.System)
            .IngestAsync("golden-cv", KnowledgeDocumentKinds.Cv, golden.Title, golden.Cv, CancellationToken.None);

        var response = await new EvidenceRetriever(knowledge, generator).SearchAsync(
            golden.Requirements.Select(r => (r.Id, r.Description)).ToList(), kind: null, Top, CancellationToken.None);

        var hits = 0;
        foreach (var (requirement, result) in golden.Requirements.Zip(response.Results))
        {
            var hit = result.Evidence.Any(e => requirement.Sections.Contains(e.Section));
            hits += hit ? 1 : 0;
            output.WriteLine($"{(hit ? "HIT " : "MISS")} {requirement.Description}");
            foreach (var evidence in result.Evidence)
            {
                output.WriteLine($"       {evidence.Score:F3} {evidence.Section}");
            }
        }

        var recall = (double)hits / golden.Requirements.Count;
        output.WriteLine($"model={response.EmbeddingModel} recall@{Top}={recall:F2}");

        var passed = recall >= MinimumRecall;
        EvalReport.Record("evidence-retrieval", $"recall@{Top}", response.EmbeddingModel, recall, $">= {MinimumRecall:F2}", passed);
        Assert.True(passed, $"recall@{Top} {recall:F2} is below {MinimumRecall}");
    }

    private sealed record GoldenCv(string Title, string Cv, IReadOnlyList<GoldenRequirement> Requirements);

    private sealed record GoldenRequirement(string Id, string Description, IReadOnlyList<string> Sections);
}
