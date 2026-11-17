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
    public void Sentence_with_an_undefined_kind_fails() =>
        Assert.Throws<CoverLetterFailedException>(() => CoverLetterGrounding.Sentences(
            new WrittenLetter([new WrittenSentence("I know Go.", (SentenceKind)7, [1])]), 3));

    [Fact]
    public void Blank_sentence_with_an_undefined_kind_is_dropped() =>
        Assert.Single(CoverLetterGrounding.Sentences(
            new WrittenLetter([new WrittenSentence(" ", (SentenceKind)7, []), new WrittenSentence("I know Go.", SentenceKind.Claim, [1])]), 3));

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

    [Fact]
    public void AlignEvidence_corrects_misnumbered_and_compound_citations_and_preserves_uncited_claims()
    {
        EvidenceItem[] evidence =
        [
            new("cv", "cv", "CV", "Jane Doe", 0, "Senior backend engineer based in Córdoba, Argentina.", 0.9),
            new("cv", "cv", "CV", "Experience > Acme Payments (2020-2025)", 1, "Designed and built payment REST APIs in C# with ASP.NET Core on .NET 8. Tuned PostgreSQL queries and indexes.", 0.9),
            new("cv", "cv", "CV", "Experience > Globex (2017-2020)", 2, "Built customer-facing single-page applications with React and TypeScript.", 0.9),
            new("cv", "cv", "CV", "Experience > Initech (2015-2017)", 3, "Containerized legacy services with Docker and moved them to Kubernetes on AWS, setting up CI/CD pipelines with GitHub Actions.", 0.9),
            new("cv", "cv", "CV", "Education", 4, "BSc in Computer Science, National University of Córdoba, 2015.", 0.9),
            new("cv", "cv", "CV", "Languages", 5, "Spanish (native), English (fluent, C1).", 0.9),
        ];

        var aligned = CoverLetterGrounding.AlignEvidence(
        [
            new("I am excited about the opportunity to contribute to your team.", SentenceKind.Connective, []),
            new("At Acme Payments I designed and built payment REST APIs in C# with ASP.NET Core on .NET 8 and tuned PostgreSQL.", SentenceKind.Claim, [3]),
            new("At Globex I built single-page applications with React and TypeScript, and at Initech I containerized legacy services with Docker and Kubernetes and CI/CD pipelines with GitHub Actions.", SentenceKind.Claim, [5]),
            new("I hold a BSc in Computer Science from the National University of Córdoba and am fluent in English at C1 level.", SentenceKind.Claim, [5]),
            new("I am excited to leverage my expertise in PostgreSQL from my work at Acme Payments.", SentenceKind.Connective, []),
            new("I designed and built payment REST APIs in C# with ASP.NET Core.", SentenceKind.Claim, []),
            new("I wrote Rust in production at Google.", SentenceKind.Claim, []),
        ],
        evidence);

        Assert.Equal(SentenceKind.Connective, aligned[0].Kind);
        Assert.Empty(aligned[0].Evidence);
        Assert.Equal([2], aligned[1].Evidence);
        Assert.Equal([3, 4], aligned[2].Evidence);
        Assert.Equal([5, 6], aligned[3].Evidence);
        Assert.Equal(SentenceKind.Claim, aligned[4].Kind);
        Assert.Equal([2], aligned[4].Evidence);
        Assert.Equal([2], aligned[5].Evidence);
        Assert.Empty(aligned[6].Evidence);
    }

    [Fact]
    public void Sentences_splits_multi_sentence_blocks_without_splitting_dotnet()
    {
        var sentences = CoverLetterGrounding.Sentences(
            new WrittenLetter(
            [
                new WrittenSentence(
                    "I am excited to apply. I built REST APIs on .NET 8 and PostgreSQL. Thank you for considering me.",
                    SentenceKind.Claim,
                    [1]),
            ]),
            evidenceCount: 1);

        Assert.Equal(3, sentences.Count);
        Assert.Equal("I am excited to apply.", sentences[0].Text);
        Assert.Equal("I built REST APIs on .NET 8 and PostgreSQL.", sentences[1].Text);
        Assert.Equal("Thank you for considering me.", sentences[2].Text);
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
        var chat = FakeChatClient.Returning("""{"quote":"","reason":"x","verdict":"supported"}""");
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
        var chat = FakeChatClient.Responding(messages => messages.Last().Text.Contains("Rust", StringComparison.Ordinal)
            ? """{"quote":"","reason":"Unsure."}"""
            : """{"quote":"Tuned PostgreSQL.","reason":"Shown.","verdict":"supported"}""");

        var verified = await new CoverLetterVerifier(chat).VerifyAsync(CoverLetterGroundingTests.Draft(1, null), CancellationToken.None);

        Assert.Equal([true, false], verified.Verdicts!.Select(v => v.Supported));
        Assert.Equal(CoverLetterVerifier.NotJudged, verified.Verdicts![1].Reason);
        Assert.Equal(2, chat.CallCount);
    }

    [Fact]
    public async Task Each_sentence_is_checked_with_only_its_cited_evidence()
    {
        var prompt = string.Empty;
        var chat = FakeChatClient.Responding(messages =>
        {
            prompt = messages.Last().Text;
            return """{"quote":"","reason":"Shown.","verdict":"supported"}""";
        });
        var draft = CoverLetterGroundingTests.Draft(1, null) with
        {
            Evidence = [CoverLetterGroundingTests.Draft(1, null).Evidence[0], new EvidenceItem("cv", "cv", "CV", "Skills", 5, "Knows Go.", 0.5)],
            Sentences = [new LetterSentence("I tuned PostgreSQL.", SentenceKind.Claim, [1])],
        };

        await new CoverLetterVerifier(chat).VerifyAsync(draft, CancellationToken.None);

        Assert.Equal(1, chat.CallCount);
        Assert.Contains("section=\"Experience\"", prompt);
        Assert.DoesNotContain("Knows Go.", prompt);
    }

    [Theory]
    [InlineData("Looks good to me.")]
    [InlineData("""{"quote":"","reason":"Shown.","verdict":7}""")]
    public async Task Invalid_verifier_output_fails(string output)
    {
        var verifier = new CoverLetterVerifier(FakeChatClient.Returning(output));

        await Assert.ThrowsAsync<CoverLetterFailedException>(() =>
            verifier.VerifyAsync(CoverLetterGroundingTests.Draft(1, null), CancellationToken.None));
    }
}
