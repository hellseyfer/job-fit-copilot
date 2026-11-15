using System.Text.Json;
using System.Text.RegularExpressions;
using CommunityToolkit.VectorData.InMemory;
using JobFit.Api.Analysis;
using JobFit.Api.CoverLetters;
using JobFit.Api.Evidence;
using JobFit.Api.Fit;
using JobFit.Api.Knowledge;
using JobFit.Api.Requirements;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace JobFit.Evals;

// Measures the cover-letter verifier on labeled sentences and checks a real letter for invented facts.
// Skipped unless OPENAI_API_KEY or OLLAMA_ENDPOINT is set.
public class CoverLetterEvals(ITestOutputHelper output)
{
    private const double MinimumAccuracy = 0.8;

    // Facts a letter must never claim: none of them is in the golden CV.
    private static readonly string[] AbsentFromCv = ["Rust", "Salesforce", "Apex", "PhD", "German", "Swift", "Google"];

    [SkippableFact]
    public async Task Verifier_accuracy_on_labeled_sentences_meets_the_target()
    {
        using var chatClient = RequirementExtractionEvals.CreateChatClient();
        Skip.If(chatClient is null, "Set OPENAI_API_KEY or OLLAMA_ENDPOINT to run the evals.");

        var golden = Load<GoldenVerification>("cover-letter-verification.json");
        var draft = new LetterDraft(
            new FitStage(new ExtractRequirementsResponse("Senior Backend Engineer", Seniority.Senior, []),
                new ScoreFitResponse(100, [], [], [], [], "unused", "unused")),
            golden.Evidence.Select((text, i) => new EvidenceItem("golden-cv", "cv", "Jane Doe", "CV", i, text, 1)).ToList(),
            Attempt: 1,
            golden.Sentences.Select(s => new LetterSentence(s.Text, s.Kind == "claim" ? SentenceKind.Claim : SentenceKind.Connective, s.Evidence)).ToList());

        var verified = await new CoverLetterVerifier(chatClient).VerifyAsync(draft, CancellationToken.None);

        var correct = 0;
        foreach (var (sentence, verdict) in golden.Sentences.Zip(verified.Verdicts!))
        {
            var agrees = sentence.Supported == verdict.Supported;
            correct += agrees ? 1 : 0;
            output.WriteLine($"{(agrees ? "OK  " : "FAIL")} {(verdict.Supported ? "supported  " : "unsupported")} {sentence.Text}");
            output.WriteLine($"       {verdict.Reason}");
        }

        var accuracy = (double)correct / golden.Sentences.Count;
        output.WriteLine($"accuracy={accuracy:F2}");
        Assert.True(accuracy >= MinimumAccuracy, $"accuracy {accuracy:F2} is below {MinimumAccuracy}");
    }

    [SkippableFact]
    public async Task Letter_for_a_strong_fit_has_no_facts_absent_from_the_cv()
    {
        using var generator = EmbeddingEvals.CreateEmbeddingGenerator();
        using var chatClient = RequirementExtractionEvals.CreateChatClient();
        Skip.If(generator is null || chatClient is null, "Set OPENAI_API_KEY or OLLAMA_ENDPOINT to run the evals.");

        var cv = Load<GoldenCv>("evidence-retrieval.json");
        var supported = Load<GoldenFit>("fit-scoring.json").Requirements.Where(r => r.Supported).ToList();
        using var store = new InMemoryCollection<string, KnowledgeChunkRecord>(KnowledgeChunkRecord.CollectionName);
        var knowledge = new KnowledgeCollection(store, NullLogger<KnowledgeCollection>.Instance);
        await new KnowledgeIngestionService(knowledge, generator, TimeProvider.System)
            .IngestAsync("golden-cv", KnowledgeDocumentKinds.Cv, cv.Title, cv.Cv, CancellationToken.None);
        var fit = await new FitScoringService(new EvidenceRetriever(knowledge, generator), new FitJudge(chatClient)).ScoreAsync(
            supported.Select(r => new FitRequirement(r.Id, r.Description, RequirementImportance.MustHave)).ToList(),
            kind: null,
            CancellationToken.None);

        var writer = new CoverLetterWriter(chatClient);
        var verifier = new CoverLetterVerifier(chatClient);
        var stage = new FitStage(new ExtractRequirementsResponse("Senior Backend Engineer", Seniority.Senior, []), fit);
        var draft = await verifier.VerifyAsync(await writer.WriteAsync(stage, CancellationToken.None), CancellationToken.None);
        while (draft.RewriteDue)
        {
            draft = await verifier.VerifyAsync(await writer.RewriteAsync(draft, CancellationToken.None), CancellationToken.None);
        }

        var letter = CoverLetterGrounding.Finalize(draft);
        output.WriteLine($"score={fit.Score} attempts={letter.Attempts}");
        output.WriteLine(letter.Text);
        foreach (var removed in letter.Removed)
        {
            output.WriteLine($"REMOVED {removed.Text} ({removed.Reason})");
        }

        var invented = AbsentFromCv.Where(term => Regex.IsMatch(letter.Text, $@"\b{term}\b", RegexOptions.IgnoreCase)).ToList();
        Assert.True(letter.Sentences.Count >= 3, $"the letter has only {letter.Sentences.Count} sentences");
        Assert.True(invented.Count == 0, $"the letter mentions {string.Join(", ", invented)}");
    }

    private static T Load<T>(string file) => JsonSerializer.Deserialize<T>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", file)), JsonSerializerOptions.Web)!;

    private sealed record GoldenVerification(IReadOnlyList<string> Evidence, IReadOnlyList<GoldenSentence> Sentences);

    private sealed record GoldenSentence(string Text, string Kind, IReadOnlyList<int> Evidence, bool Supported);

    private sealed record GoldenCv(string Title, string Cv);

    private sealed record GoldenFit(IReadOnlyList<GoldenFitRequirement> Requirements);

    private sealed record GoldenFitRequirement(string Id, string Description, bool Supported);
}
