using JobFit.Api.Analysis;
using JobFit.Api.Evidence;

namespace JobFit.Api.CoverLetters;

public sealed record CoverLetter(
    string Text, IReadOnlyList<CoverLetterSentence> Sentences, IReadOnlyList<RemovedSentence> Removed, int Attempts);

public sealed record CoverLetterSentence(string Text, IReadOnlyList<EvidenceItem> Evidence);

public sealed record RemovedSentence(string Text, string Reason);

public enum SentenceKind { Claim, Connective }

// A sentence of a draft; Evidence holds valid one-based numbers into LetterDraft.Evidence.
public sealed record LetterSentence(string Text, SentenceKind Kind, IReadOnlyList<int> Evidence);

public sealed record SentenceVerdict(bool Supported, string Reason);

// The loop state: one immutable draft per write, with verdicts once it is verified.
public sealed record LetterDraft(
    FitStage Stage,
    IReadOnlyList<EvidenceItem> Evidence,
    int Attempt,
    IReadOnlyList<LetterSentence> Sentences,
    IReadOnlyList<SentenceVerdict>? Verdicts = null)
{
    public const int MaxDrafts = 3;

    public bool RewriteDue => Verdicts is not null && Verdicts.Any(v => !v.Supported) && Attempt < MaxDrafts;
}

public sealed record WrittenLetter(IReadOnlyList<WrittenSentence?>? Sentences);

public sealed record WrittenSentence(string? Text, SentenceKind? Kind, IReadOnlyList<int>? Evidence);

public sealed record VerifiedLetter(IReadOnlyList<VerifiedSentence?>? Verdicts);

// Reason comes before the decision so the model explains before it decides.
public sealed record VerifiedSentence(int? Sentence, string? Reason, bool? Supported);

public sealed class CoverLetterFailedException(string message, Exception? innerException = null)
    : Exception(message, innerException);
