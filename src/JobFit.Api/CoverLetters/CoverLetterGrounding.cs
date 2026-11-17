using System.Text.RegularExpressions;
using JobFit.Api.Evidence;
using JobFit.Api.Fit;

namespace JobFit.Api.CoverLetters;

public static partial class CoverLetterGrounding
{
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "in", "at", "on", "to", "of", "by", "as", "or", "an", "be", "is", "it", "my", "me", "we", "us", "up", "so", "no",
        "the", "and", "for", "with", "from", "that", "this", "have", "has", "had", "was", "were", "are",
        "been", "being", "into", "over", "under", "through", "about", "such", "their", "them", "they",
        "our", "your", "you", "who", "what", "where", "when", "which", "while", "also", "very", "more",
        "most", "some", "any", "all", "both", "each", "other", "another", "only", "same", "than", "then",
        "too", "will", "would", "can", "could", "should", "may", "might", "must", "shall", "role",
        "position", "team", "company", "work", "working", "worked", "experience", "experienced", "years",
        "year", "built", "building", "build", "using", "used", "use", "including", "include", "includes",
        "skills", "skill", "strong", "solid", "senior", "junior", "mid", "lead", "engineer", "developer",
        "backend", "frontend", "software", "development", "services", "service", "system", "systems",
        "application", "applications", "opportunity", "contribute", "excited", "eager", "look", "forward",
        "discussing", "thank", "considering", "qualified", "highly", "background", "align", "requirements",
        "based", "level", "essential", "hold", "holds",
    };
    // The chunks cited by met and partial assessments, each once, in assessment order; numbered from 1.
    public static IReadOnlyList<EvidenceItem> NumberEvidence(IReadOnlyList<RequirementAssessment> assessments) =>
        assessments
            .Where(a => a.Status != FitStatus.Missing)
            .SelectMany(a => a.Evidence)
            .DistinctBy(e => (e.DocumentId, e.ChunkIndex))
            .ToList();

    public static int NumberOf(IReadOnlyList<EvidenceItem> evidence, EvidenceItem item)
    {
        for (var i = 0; i < evidence.Count; i++)
        {
            if (evidence[i].DocumentId == item.DocumentId && evidence[i].ChunkIndex == item.ChunkIndex)
            {
                return i + 1;
            }
        }

        return 0;
    }

    // Drops blank sentences and citations outside 1..evidenceCount; a sentence without a kind is treated as a claim.
    public static IReadOnlyList<LetterSentence> Sentences(WrittenLetter written, int evidenceCount)
    {
        if ((written.Sentences ?? []).Any(s => !string.IsNullOrWhiteSpace(s?.Text) && s.Kind is { } kind && !Enum.IsDefined(kind)))
        {
            throw new CoverLetterFailedException("The cover letter draft has a sentence with an unknown kind.");
        }

        var sentences = (written.Sentences ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s?.Text))
            .Select(s => new LetterSentence(
                s!.Text!.Trim(),
                s.Kind ?? SentenceKind.Claim,
                (s.Evidence ?? []).Where(n => n >= 1 && n <= evidenceCount).Distinct().ToList()))
            .ToList();

        return sentences.Count > 0
            ? sentences
            : throw new CoverLetterFailedException("The cover letter draft has no sentences.");
    }

    // Aligns cited claims (and connective sentences that state evidence facts) with the evidence items whose
    // distinctive terms appear in the sentence, correcting off-by-one or partial citations from small models.
    public static IReadOnlyList<LetterSentence> AlignEvidence(
        IReadOnlyList<LetterSentence> sentences, IReadOnlyList<EvidenceItem> evidence)
    {
        if (evidence.Count == 0)
        {
            return sentences;
        }

        var itemTokens = evidence.Select(e => Tokens($"{e.Section} {e.Text}")).ToList();
        return sentences.Select(sentence =>
        {
            var tokens = Tokens(sentence.Text);
            var strong = MatchingNumbers(tokens, itemTokens, minimumOverlap: 2);
            if (strong.Count > 0)
            {
                return new LetterSentence(sentence.Text, SentenceKind.Claim, strong);
            }

            if (sentence.Kind == SentenceKind.Claim && sentence.Evidence.Count > 0)
            {
                var matched = MatchingNumbers(tokens, itemTokens, minimumOverlap: 1);
                return matched.Count > 0 ? sentence with { Evidence = matched } : sentence;
            }

            return sentence;
        }).ToList();
    }

    private static List<int> MatchingNumbers(
        HashSet<string> sentenceTokens, List<HashSet<string>> itemTokens, int minimumOverlap) =>
        Enumerable.Range(0, itemTokens.Count)
            .Where(i => sentenceTokens.Count(t => itemTokens[i].Contains(t)) >= minimumOverlap)
            .Select(i => i + 1)
            .ToList();

    private static HashSet<string> Tokens(string text) =>
        TokenPattern().Matches(text.ToLowerInvariant())
            .Select(m => m.Value.Trim('.', '/', '-'))
            .Where(t => t.Length >= 2 && !Stopwords.Contains(t))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"[\p{L}\p{N}#+./-]{2,}")]
    private static partial Regex TokenPattern();

    // Keeps the supported sentences of a verified draft and reports the others.
    public static CoverLetter Finalize(LetterDraft draft)
    {
        var verdicts = draft.Verdicts ?? throw new InvalidOperationException("The draft has not been verified.");
        var kept = new List<CoverLetterSentence>();
        var removed = new List<RemovedSentence>();
        for (var i = 0; i < draft.Sentences.Count; i++)
        {
            var sentence = draft.Sentences[i];
            if (verdicts[i].Supported)
            {
                kept.Add(new CoverLetterSentence(sentence.Text, sentence.Evidence.Select(n => draft.Evidence[n - 1]).ToList()));
            }
            else
            {
                removed.Add(new RemovedSentence(sentence.Text, verdicts[i].Reason));
            }
        }

        return new CoverLetter(string.Join(" ", kept.Select(s => s.Text)), kept, removed, draft.Attempt);
    }
}
