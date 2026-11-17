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
        return PhonePattern().Replace(masked, match => IsPhone(match.Value) ? Phone : match.Value);
    }

    private static bool IsPhone(string candidate)
    {
        var digits = candidate.Count(char.IsAsciiDigit);
        return digits is >= 8 and <= 15 && !YearRangePattern().IsMatch(candidate) && !DatePattern().IsMatch(candidate);
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"\b(?:https?://|www\.)[^\s<>()\[\]""']+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"(?<![\w+.])\+?\d[\d \t().-]{6,}\d(?![\w%])")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"^\d{4}\s*[-–]\s*\d{4}$")]
    private static partial Regex YearRangePattern();

    [GeneratedRegex(@"^\d{4}[-./]\d{1,2}[-./]\d{1,2}$|^\d{1,2}[-./]\d{1,2}[-./]\d{4}$")]
    private static partial Regex DatePattern();
}
