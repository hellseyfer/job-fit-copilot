using System.Text;
using System.Text.RegularExpressions;

namespace JobFit.Api.Guardrails;

// First guardrail layer: narrow, deterministic patterns. Anything they miss goes to the classifier.
public static partial class InjectionPatterns
{
    // The tags the API's prompts use to delimit untrusted text; a text that contains one tries to break out of it.
    public static readonly string[] PromptDelimiters =
    [
        "job_posting", "evidence", "requirement", "sentence", "note", "job_title",
        "previous_draft", "rejected_letter", "reviewer_feedback", "untrusted_text",
    ];

    public static bool Matches(string text)
    {
        var normalized = Normalize(text);
        return Overrides().IsMatch(normalized)
            || RoleMarker().IsMatch(normalized)
            || TemplateToken().IsMatch(normalized)
            || DelimiterTag().IsMatch(normalized);
    }

    // Undoes trivial obfuscation: compatibility forms (full-width letters), zero-width characters and spacing.
    public static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormKC))
        {
            if (c is not ('\u200B' or '\u200C' or '\u200D' or '\u2060' or '\uFEFF' or '\u00AD'))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return HorizontalSpace().Replace(builder.ToString(), " ");
    }

    [GeneratedRegex(
        @"\b(?:ignore|disregard|forget|override)\b[^.\n]{0,40}?\b(?:previous|prior|above|earlier|all|any|your|the|these|those)\b[^.\n]{0,20}?\b(?:instructions?|prompts?|rules|directions|guidelines)\b"
        + @"|\byou are now\b|\bnew instructions\s*:|\b(?:reveal|print|show|repeat|output)\b[^.\n]{0,20}\bsystem prompt\b|\bsystem prompt\s*:")]
    private static partial Regex Overrides();

    [GeneratedRegex(@"^[ ]?(?:system|assistant|developer)[ ]?:", RegexOptions.Multiline)]
    private static partial Regex RoleMarker();

    [GeneratedRegex(@"<\|[a-z_]+\|>|\[/?inst\]|<</?sys>>")]
    private static partial Regex TemplateToken();

    [GeneratedRegex(@"</?[ ]?(?:job_posting|evidence|requirement|sentence|note|job_title|previous_draft|rejected_letter|reviewer_feedback|untrusted_text)[ ]?>")]
    private static partial Regex DelimiterTag();

    [GeneratedRegex(@"[ \t\u00A0]+")]
    private static partial Regex HorizontalSpace();
}
