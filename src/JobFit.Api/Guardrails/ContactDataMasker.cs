using System.Text.RegularExpressions;

namespace JobFit.Api.Guardrails;

// Replaces the candidate's contact data before any text reaches a model or the vector store.
public static partial class ContactDataMasker
{
    public const string Email = "[email]";
    public const string Phone = "[phone]";
    public const string Url = "[url]";

    public static string Mask(string text)
    {
        var masked = EmailPattern().Replace(text, Email);
        masked = UrlPattern().Replace(masked, match =>
        {
            var trailing = match.Value.Length - match.Value.TrimEnd('.', ',', ';', ':', '!', '?').Length;
            return Url + match.Value[(match.Value.Length - trailing)..];
        });
        return MaskPhones(masked);
    }

    private static string MaskPhones(string text) => PhonePattern().Replace(text, match =>
    {
        if (IsPhone(match.Value))
            return Phone;
        // Numbers written next to each other match as one candidate that is too long; try each part.
        return match.Value.Count(char.IsAsciiDigit) > 15 && match.Value.Contains(' ')
            ? WhitespacePart().Replace(match.Value, part => MaskPhones(part.Value))
            : match.Value;
    });

    private static bool IsPhone(string candidate)
    {
        var digits = candidate.Count(char.IsAsciiDigit);
        return digits is >= 8 and <= 15 && !YearRangePattern().IsMatch(candidate) && !DatePattern().IsMatch(candidate);
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\b(?:https?://|www\.)[^\s<>()\[\]""']+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    // Digit groups joined by single separators, with an optional country code and area code in parentheses.
    [GeneratedRegex(@"(?<![\w+.])(?:\+\d{1,3}[ .-]?)?(?:\(\d{1,4}\)[ .-]?)?\d+(?:[ .-]\d+){0,5}(?![\w%])")]
    private static partial Regex PhonePattern();

    // Two years next to each other: a range such as 2020-2025, possibly followed by a duration.
    [GeneratedRegex(@"(?<!\d)(?:19|20)\d\d[ .-](?:19|20)\d\d(?!\d)")]
    private static partial Regex YearRangePattern();

    [GeneratedRegex(@"\S+")]
    private static partial Regex WhitespacePart();

    [GeneratedRegex(@"^\d{4}[-./]\d{1,2}[-./]\d{1,2}$|^\d{1,2}[-./]\d{1,2}[-./]\d{4}$")]
    private static partial Regex DatePattern();
}
