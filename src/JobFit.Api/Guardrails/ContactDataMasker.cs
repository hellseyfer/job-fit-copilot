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
        return match.Value.Count(char.IsAsciiDigit) > 15 && match.Value.Any(char.IsWhiteSpace)
            ? WhitespacePart().Replace(match.Value, part => MaskPhones(part.Value))
            : match.Value;
    });

    private static bool IsPhone(string candidate)
    {
        var digits = candidate.Count(char.IsAsciiDigit);
        return digits is >= 8 and <= 15 && !IsYearRange(candidate) && !DatePattern().IsMatch(candidate);
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\b(?:https?://|www\.)[^\s<>()\[\]""']+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    // Digit groups joined by a dot, a hyphen or spaces, with an optional country code and area code in parentheses.
    [GeneratedRegex(@"(?<![\w+.])(?:\+\d{1,3}(?:[.-]|[ \t]+)?)?(?:\(\d{1,4}\)(?:[.-]|[ \t]+)?)?\d+(?:(?:[.-]|[ \t]+)\d+){0,5}(?![\w%])")]
    private static partial Regex PhonePattern();

    // Two years next to each other, the second not earlier: a range such as 2020-2025 or 1850-1900.
    private static bool IsYearRange(string candidate) => FourDigitPair().Matches(candidate).Any(pair =>
    {
        var (from, to) = (int.Parse(pair.Groups[1].Value), int.Parse(pair.Groups[2].Value));
        return from >= 1000 && to <= 2099 && to >= from;
    });

    [GeneratedRegex(@"(?<!\d)(\d{4})(?:[.-]|[ \t]+)(?=(\d{4})(?!\d))")]
    private static partial Regex FourDigitPair();

    [GeneratedRegex(@"\S+")]
    private static partial Regex WhitespacePart();

    [GeneratedRegex(@"^\d{4}[-./]\d{1,2}[-./]\d{1,2}$|^\d{1,2}[-./]\d{1,2}[-./]\d{4}$")]
    private static partial Regex DatePattern();
}
