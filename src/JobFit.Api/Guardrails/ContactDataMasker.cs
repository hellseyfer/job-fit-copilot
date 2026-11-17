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
        // A date or year range at the start is kept, and the digits after it are checked on their own.
        var leading = LeadingDate().Match(match.Value);
        if (leading.Success && IsDate(leading))
            return leading.Value + MaskPhones(match.Value[leading.Length..]);
        // Eight digits or more is a phone; several phones written together are masked as one.
        return match.Value.Count(char.IsAsciiDigit) >= 8 ? Phone : match.Value;
    });

    private static bool IsDate(Match leading)
    {
        if (!leading.Groups["from"].Success)
            return true;
        var (from, to) = (int.Parse(leading.Groups["from"].Value), int.Parse(leading.Groups["to"].Value));
        return from >= 1000 && to <= 2099 && to >= from;
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\b(?:https?://|www\.)[^\s<>()\[\]""']+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    // Digit groups joined by a dot, a hyphen or spaces, with an optional country code and area code in parentheses.
    [GeneratedRegex(@"(?<![\w+.])(?:\+\d{1,3}(?:[.-]|[ \t]+)?)?(?:\(\d{1,4}\)(?:[.-]|[ \t]+)?)?\d+(?:(?:[.-]|[ \t]+)\d+){0,5}(?![\w%])")]
    private static partial Regex PhonePattern();

    // A date (2024-01-15, 15/01/2024) or a year range, the second year not earlier (2020-2025, 1850-1900).
    [GeneratedRegex(@"^(?:\d{4}[-./]\d{1,2}[-./]\d{1,2}|\d{1,2}[-./]\d{1,2}[-./]\d{4}|(?<from>\d{4})(?:[.-]|[ \t]+)(?<to>\d{4}))(?!\d)")]
    private static partial Regex LeadingDate();
}
