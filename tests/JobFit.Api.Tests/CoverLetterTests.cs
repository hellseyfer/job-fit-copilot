using JobFit.Api.Analysis;
using JobFit.Api.CoverLetters;
using JobFit.Api.Evidence;
using JobFit.Api.Fit;
using JobFit.Api.Requirements;

namespace JobFit.Api.Tests;

public class CoverLetterGroundingTests
{
    private static readonly EvidenceItem Pg = Item(0, "Tuned PostgreSQL.");
    private static readonly EvidenceItem K8s = Item(1, "Ran Kubernetes.");
    private static readonly EvidenceItem Rust = Item(2, "Read about Rust.");

    [Fact]
    public void Evidence_comes_from_met_and_partial_assessments_once_each()
    {
        var evidence = CoverLetterGrounding.NumberEvidence(
        [
            Assessment("req-1", FitStatus.Met, Pg, K8s),
            Assessment("req-2", FitStatus.Missing, Rust),
            Assessment("req-3", FitStatus.Partial, K8s),
        ]);

        Assert.Equal([Pg, K8s], evidence);
        Assert.Equal(2, CoverLetterGrounding.NumberOf(evidence, K8s));
    }

    [Fact]
    public void Out_of_range_and_duplicate_citations_are_dropped()
    {
        var sentences = CoverLetterGrounding.Sentences(
            new WrittenLetter([new WrittenSentence("I tuned PostgreSQL.", SentenceKind.Claim, [1, 9, 1]), new WrittenSentence(" ", null, [])]),
            evidenceCount: 3);

        var sentence = Assert.Single(sentences);
        Assert.Equal([1], sentence.Evidence);
    }

    [Fact]
    public void Sentence_without_kind_is_a_claim()
    {
        var sentences = CoverLetterGrounding.Sentences(new WrittenLetter([new WrittenSentence("I know Go.", null, null)]), 3);

        Assert.Equal(SentenceKind.Claim, Assert.Single(sentences).Kind);
    }

    [Fact]
    public void Draft_without_sentences_fails() =>
        Assert.Throws<CoverLetterFailedException>(() => CoverLetterGrounding.Sentences(new WrittenLetter([]), 3));

    [Fact]
    public void Finalize_keeps_supported_sentences_and_reports_the_rest()
    {
        var draft = Draft(attempt: 3, [Supported, new SentenceVerdict(false, "Not in the evidence.")]);

        var letter = CoverLetterGrounding.Finalize(draft);

        Assert.Equal("I tuned PostgreSQL.", letter.Text);
        Assert.Equal(Pg, Assert.Single(Assert.Single(letter.Sentences).Evidence));
        Assert.Equal(new RemovedSentence("I wrote Rust in production.", "Not in the evidence."), Assert.Single(letter.Removed));
        Assert.Equal(3, letter.Attempts);
    }

    [Theory]
    [InlineData(1, false, true)]
    [InlineData(2, false, true)]
    [InlineData(3, false, false)]
    [InlineData(1, true, false)]
    public void Rewrite_is_due_while_unsupported_and_below_three_drafts(int attempt, bool allSupported, bool due)
    {
        var draft = Draft(attempt, [Supported, allSupported ? Supported : new SentenceVerdict(false, "No.")]);

        Assert.Equal(due, draft.RewriteDue);
    }

    private static readonly SentenceVerdict Supported = new(true, "Shown.");

    internal static LetterDraft Draft(int attempt, IReadOnlyList<SentenceVerdict>? verdicts) => new(
        Stage(),
        [Pg],
        attempt,
        [new LetterSentence("I tuned PostgreSQL.", SentenceKind.Claim, [1]), new LetterSentence("I wrote Rust in production.", SentenceKind.Claim, [1])],
        verdicts);

    internal static FitStage Stage() => new(
        new ExtractRequirementsResponse("Backend engineer", Seniority.Mid, []),
        new ScoreFitResponse(100, [], [], [], [], "model", "embeddings"));

    private static RequirementAssessment Assessment(string id, FitStatus status, params EvidenceItem[] evidence) =>
        new(id, id, RequirementImportance.MustHave, status, "rationale", evidence);

    private static EvidenceItem Item(int index, string text) => new("cv", "cv", "CV", "Experience", index, text, 0.9);
}

public class CoverLetterVerifierTests
{
    [Fact]
    public async Task Uncited_claim_is_unsupported_without_a_verifier_call()
    {
        var chat = FakeChatClient.Returning("""{"verdicts":[]}""");
        var draft = CoverLetterGroundingTests.Draft(1, null) with
        {
            Sentences = [new LetterSentence("I am an expert in everything.", SentenceKind.Claim, [])],
        };

        var verified = await new CoverLetterVerifier(chat).VerifyAsync(draft, CancellationToken.None);

        Assert.Equal(new SentenceVerdict(false, CoverLetterVerifier.UncitedClaim), Assert.Single(verified.Verdicts!));
        Assert.Equal(0, chat.CallCount);
    }

    [Fact]
    public async Task Sentence_without_a_verdict_is_unsupported()
    {
        var chat = FakeChatClient.Returning("""{"verdicts":[{"sentence":1,"reason":"Shown.","supported":true}]}""");

        var verified = await new CoverLetterVerifier(chat).VerifyAsync(CoverLetterGroundingTests.Draft(1, null), CancellationToken.None);

        Assert.Equal([true, false], verified.Verdicts!.Select(v => v.Supported));
        Assert.Equal(CoverLetterVerifier.NotJudged, verified.Verdicts![1].Reason);
        Assert.Equal(1, chat.CallCount);
    }

    [Fact]
    public async Task Invalid_verifier_output_fails()
    {
        var verifier = new CoverLetterVerifier(FakeChatClient.Returning("Looks good to me."));

        await Assert.ThrowsAsync<CoverLetterFailedException>(() =>
            verifier.VerifyAsync(CoverLetterGroundingTests.Draft(1, null), CancellationToken.None));
    }
}
