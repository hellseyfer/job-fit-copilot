using JobFit.Api.Evidence;
using JobFit.Api.Fit;

namespace JobFit.Api.CoverLetters;

public static class CoverLetterGrounding
{
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
