using System.Text;
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

    // A run of digit groups may hold phones, dates and year ranges side by side. Whole dates and ranges are kept;
    // every other part with 8 digits or more is a phone (several phones written together are masked as one).
    private static string MaskPhones(string text) => PhonePattern().Replace(text, match =>
    {
        var candidate = match.Value;
        var masked = new StringBuilder();
        var position = 0;
        foreach (Match date in DateToken().Matches(candidate))
        {
            if (!IsDate(date))
                continue;
            masked.Append(MaskSegment(candidate[position..date.Index])).Append(date.Value);
            position = date.Index + date.Length;
        }
        return masked.Append(MaskSegment(candidate[position..])).ToString();
    });

    private static string MaskSegment(string segment)
    {
        if (segment.Count(char.IsAsciiDigit) < 8)
            return segment;
        var core = segment.Trim();
        var start = segment.IndexOf(core, StringComparison.Ordinal);
        return segment[..start] + Phone + segment[(start + core.Length)..];
    }

    private static bool IsDate(Match date)
    {
        if (date.Groups["from"].Success)
        {
            var (from, to) = (int.Parse(date.Groups["from"].Value), int.Parse(date.Groups["to"].Value));
            return from >= 1000 && to <= 2099 && to - from is >= 0 and <= 100;
        }
        var (year, month, day) = date.Groups["year"].Success
            ? (date.Groups["year"].Value, date.Groups["month"].Value, date.Groups["day"].Value)
            : (date.Groups["year2"].Value, date.Groups["month2"].Value, date.Groups["day2"].Value);
        return int.Parse(year) is >= 1900 and <= 2099 && int.Parse(month) is >= 1 and <= 12 && int.Parse(day) is >= 1 and <= 31;
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\b(?:https?://|www\.)[^\s<>()\[\]""']+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    // Digit groups joined by a dot, a hyphen or spaces, with an optional country code and area code in parentheses.
    [GeneratedRegex(@"(?<![\w+.])(?:\+\d{1,3}(?:[.-]|[ \t]+)?)?(?:\(\d{1,4}\)(?:[.-]|[ \t]+)?)?\d+(?:(?:[.-]|[ \t]+)\d+){0,5}(?![\w%])")]
    private static partial Regex PhonePattern();

    // A whole whitespace-separated date (2024-01-15, 15.01.2024) or year range (2020-2025) inside a candidate.
    [GeneratedRegex(@"(?<!\S)(?:(?<year>\d{4})(?<s>[.-])(?<month>\d{1,2})\k<s>(?<day>\d{1,2})|(?<day2>\d{1,2})(?<s2>[.-])(?<month2>\d{1,2})\k<s2>(?<year2>\d{4})|(?<from>\d{4})-(?<to>\d{4}))(?!\S)")]
    private static partial Regex DateToken();
}
