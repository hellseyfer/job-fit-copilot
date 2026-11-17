using System.Text.Json;
using System.Text.RegularExpressions;
using CommunityToolkit.VectorData.InMemory;
using JobFit.Api.Analysis;
using JobFit.Api.CoverLetters;
using JobFit.Api.Evidence;
using JobFit.Api.Fit;
using JobFit.Api.Guardrails;
using JobFit.Api.Knowledge;
using JobFit.Api.Requirements;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace JobFit.Evals;

// Runs the full analysis pipeline (guardrails -> extraction -> retrieval -> fit scoring -> route -> cover letter or study plan)
// over a golden dataset with real chat and embedding models on an in-memory vector store.
[Collection("Evals")]
public class EndToEndEvals(ITestOutputHelper output)
{
    private const double MinimumMustHaveRecall = 0.75;
    private const double MinimumStudyPlanCoverage = 0.8;
    private const int DefaultFitThreshold = 70;

    [SkippableFact]
    public async Task Strong_fit_golden_posting_produces_a_grounded_cover_letter()
    {
        using var generator = EvalHarness.RequireEmbeddingGenerator();
        using var chatClient = EvalHarness.RequireChatClient();
        var model = EvalHarness.ChatModelName();

        var golden = LoadGolden();
        var posting = golden.Cases.Single(c => c.ExpectedOutcome == "strong-fit");
        using var store = new InMemoryCollection<string, KnowledgeChunkRecord>(KnowledgeChunkRecord.CollectionName);
        var knowledge = await SeedKnowledgeAsync(golden, store, generator, chatClient);

        var guard = CreateInputGuard(chatClient);
        await guard.ScreenAsync("jobPosting", posting.JobPosting, CancellationToken.None);

        var extracted = await new RequirementExtractor(chatClient, NullLogger<RequirementExtractor>.Instance)
            .ExtractAsync(posting.JobPosting, CancellationToken.None);
        var mustHaveRecall = MustHaveRecall(extracted, posting.MustHave);

        var fit = await new FitScoringService(new EvidenceRetriever(knowledge, generator), new FitJudge(chatClient))
            .ScoreAsync(
                extracted.Requirements.Select(r => new FitRequirement(r.Id, r.Description, r.Importance)).ToList(),
                kind: null,
                CancellationToken.None);

        var isStrongFit = JobAnalysisWorkflow.IsStrongFit(fit.Score, DefaultFitThreshold);
        var route = isStrongFit ? AnalysisRoute.StrongFit : AnalysisRoute.WeakFit;

        output.WriteLine($"{posting.Name}: mustHaveRecall={mustHaveRecall:F2} score={fit.Score} route={route}");

        EvalReport.Record(
            "end-to-end",
            $"{posting.Name} must-have recall",
            model,
            mustHaveRecall,
            $">= {MinimumMustHaveRecall:F2}",
            mustHaveRecall >= MinimumMustHaveRecall);
        EvalReport.Record(
            "end-to-end",
            $"{posting.Name} strong-fit route",
            model,
            isStrongFit ? 1 : 0,
            "== 1.00",
            isStrongFit);

        Assert.True(mustHaveRecall >= MinimumMustHaveRecall, $"Must-have recall {mustHaveRecall:F2} is below {MinimumMustHaveRecall}.");
        Assert.Equal(AnalysisRoute.StrongFit, route);

        var writer = new CoverLetterWriter(chatClient);
        var verifier = new CoverLetterVerifier(chatClient);
        var stage = new FitStage(extracted, fit);
        var draft = await verifier.VerifyAsync(await writer.WriteAsync(stage, CancellationToken.None), CancellationToken.None);
        while (draft.RewriteDue)
        {
            draft = await verifier.VerifyAsync(await writer.RewriteAsync(draft, CancellationToken.None), CancellationToken.None);
        }

        var letter = CoverLetterGrounding.Finalize(draft);
        output.WriteLine(letter.Text);

        var retrievedKeys = fit.Assessments
            .Where(a => a.Status is FitStatus.Met or FitStatus.Partial)
            .SelectMany(a => a.Evidence)
            .Select(e => (e.DocumentId, e.ChunkIndex))
            .ToHashSet();
        var citationsValid = letter.Sentences.All(s => s.Evidence.All(e => retrievedKeys.Contains((e.DocumentId, e.ChunkIndex))));
        var invented = golden.AbsentFromKnowledge
            .Where(term => Regex.IsMatch(letter.Text, $@"\b{Regex.Escape(term)}\b", RegexOptions.IgnoreCase))
            .ToList();
        var letterGrounded = letter.Sentences.Count >= 3 && citationsValid && invented.Count == 0;

        EvalReport.Record(
            "end-to-end",
            $"{posting.Name} cover-letter grounding",
            model,
            letterGrounded ? 1 : 0,
            "== 1.00",
            letterGrounded);

        Assert.True(letter.Sentences.Count >= 3, $"Cover letter has only {letter.Sentences.Count} sentences.");
        Assert.True(citationsValid, "Cover letter cites evidence not accepted by the fit judge.");
        Assert.True(invented.Count == 0, $"Cover letter mentions absent terms: {string.Join(", ", invented)}");
    }

    [SkippableFact]
    public async Task Weak_fit_golden_posting_takes_the_weak_fit_route_and_covers_gaps()
    {
        using var generator = EvalHarness.RequireEmbeddingGenerator();
        using var chatClient = EvalHarness.RequireChatClient();
        var model = EvalHarness.ChatModelName();

        var golden = LoadGolden();
        var posting = golden.Cases.Single(c => c.ExpectedOutcome == "weak-fit");
        using var store = new InMemoryCollection<string, KnowledgeChunkRecord>(KnowledgeChunkRecord.CollectionName);
        var knowledge = await SeedKnowledgeAsync(golden, store, generator, chatClient);

        var guard = CreateInputGuard(chatClient);
        await guard.ScreenAsync("jobPosting", posting.JobPosting, CancellationToken.None);

        var extracted = await new RequirementExtractor(chatClient, NullLogger<RequirementExtractor>.Instance)
            .ExtractAsync(posting.JobPosting, CancellationToken.None);
        var mustHaveRecall = MustHaveRecall(extracted, posting.MustHave);

        var fit = await new FitScoringService(new EvidenceRetriever(knowledge, generator), new FitJudge(chatClient))
            .ScoreAsync(
                extracted.Requirements.Select(r => new FitRequirement(r.Id, r.Description, r.Importance)).ToList(),
                kind: null,
                CancellationToken.None);

        var isWeakFit = !JobAnalysisWorkflow.IsStrongFit(fit.Score, DefaultFitThreshold);
        var route = isWeakFit ? AnalysisRoute.WeakFit : AnalysisRoute.StrongFit;
        var plan = await new StudyPlanner(chatClient).PlanAsync(fit.Assessments, CancellationToken.None);

        var gaps = fit.Assessments.Where(a => a.Status != FitStatus.Met).ToList();
        var planned = plan.Select(i => i.RequirementId).ToHashSet();
        var coverage = gaps.Count == 0 ? 0 : (double)gaps.Count(g => planned.Contains(g.RequirementId)) / gaps.Count;

        output.WriteLine($"{posting.Name}: mustHaveRecall={mustHaveRecall:F2} score={fit.Score} route={route} gaps={gaps.Count} coverage={coverage:F2}");

        EvalReport.Record(
            "end-to-end",
            $"{posting.Name} must-have recall",
            model,
            mustHaveRecall,
            $">= {MinimumMustHaveRecall:F2}",
            mustHaveRecall >= MinimumMustHaveRecall);
        EvalReport.Record(
            "end-to-end",
            $"{posting.Name} weak-fit route",
            model,
            isWeakFit ? 1 : 0,
            "== 1.00",
            isWeakFit);
        EvalReport.Record(
            "end-to-end",
            $"{posting.Name} study-plan coverage",
            model,
            coverage,
            $">= {MinimumStudyPlanCoverage:F2}",
            coverage >= MinimumStudyPlanCoverage);

        Assert.True(mustHaveRecall >= MinimumMustHaveRecall, $"Must-have recall {mustHaveRecall:F2} is below {MinimumMustHaveRecall}.");
        Assert.Equal(AnalysisRoute.WeakFit, route);
        Assert.True(gaps.Count > 0, "The weak-fit golden posting must leave gaps.");
        Assert.True(coverage >= MinimumStudyPlanCoverage, $"Study-plan gap coverage {coverage:F2} is below {MinimumStudyPlanCoverage}.");
    }

    [SkippableFact]
    public async Task Injected_golden_posting_is_rejected_before_extraction()
    {
        using var chatClient = EvalHarness.RequireChatClient();
        var model = EvalHarness.ChatModelName();

        var golden = LoadGolden();
        var posting = golden.Cases.Single(c => c.ExpectedOutcome == "prompt-injection");
        var guard = CreateInputGuard(chatClient);

        var exception = await Record.ExceptionAsync(() =>
            guard.ScreenAsync("jobPosting", posting.JobPosting, CancellationToken.None));
        var blocked = exception is PromptInjectionException;

        output.WriteLine($"{posting.Name}: blocked={blocked} ({exception?.GetType().Name ?? "none"})");
        EvalReport.Record("end-to-end", $"{posting.Name} blocked", model, blocked ? 1 : 0, "== 1.00", blocked);

        Assert.IsType<PromptInjectionException>(exception);
    }

    private static async Task<KnowledgeCollection> SeedKnowledgeAsync(
        GoldenEndToEnd golden,
        InMemoryCollection<string, KnowledgeChunkRecord> store,
        IEmbeddingGenerator<string, Embedding<float>> generator,
        IChatClient chatClient)
    {
        var cv = JsonSerializer.Deserialize<GoldenCv>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "evidence-retrieval.json")),
            JsonSerializerOptions.Web)!;
        var guard = CreateInputGuard(chatClient);
        var knowledge = new KnowledgeCollection(store, NullLogger<KnowledgeCollection>.Instance);
        var ingestion = new KnowledgeIngestionService(knowledge, generator, TimeProvider.System);

        var maskedCv = ContactDataMasker.Mask(cv.Cv);
        await guard.ScreenAsync("content", maskedCv, CancellationToken.None);
        await ingestion.IngestAsync("golden-cv", KnowledgeDocumentKinds.Cv, cv.Title, maskedCv, CancellationToken.None);

        var maskedProject = ContactDataMasker.Mask(golden.Project.Content);
        await guard.ScreenAsync("content", maskedProject, CancellationToken.None);
        await ingestion.IngestAsync(
            golden.Project.Id,
            KnowledgeDocumentKinds.Project,
            golden.Project.Title,
            maskedProject,
            CancellationToken.None);

        return knowledge;
    }

    private static InputGuard CreateInputGuard(IChatClient chatClient) =>
        new(new InjectionClassifier(chatClient), Options.Create(new GuardrailOptions { Classifier = true }));

    private static double MustHaveRecall(ExtractRequirementsResponse extracted, IReadOnlyList<string> expectedMustHave) =>
        expectedMustHave.Count == 0
            ? 1.0
            : (double)expectedMustHave.Count(k => RequirementExtractionEvals.Mentions(extracted, k, RequirementImportance.MustHave))
              / expectedMustHave.Count;

    private static GoldenEndToEnd LoadGolden() =>
        JsonSerializer.Deserialize<GoldenEndToEnd>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "end-to-end.json")),
            JsonSerializerOptions.Web)!;

    private sealed record GoldenCv(string Title, string Cv);

    private sealed record GoldenProject(string Id, string Title, string Content);

    private sealed record GoldenCase(string Name, string ExpectedOutcome, string JobPosting, IReadOnlyList<string> MustHave);

    private sealed record GoldenEndToEnd(
        GoldenProject Project,
        IReadOnlyList<string> AbsentFromKnowledge,
        IReadOnlyList<GoldenCase> Cases);
}
