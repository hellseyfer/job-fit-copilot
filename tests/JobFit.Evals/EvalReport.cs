using System.Globalization;
using System.Text;
using System.Text.Json;

namespace JobFit.Evals;

internal sealed record EvalEntry(
    string Suite,
    string Metric,
    string Model,
    double Value,
    string Threshold,
    bool Passed);

internal static class EvalReport
{
    private static readonly Lock Sync = new();
    private static readonly List<EvalEntry> Entries = [];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    internal static void Record(
        string suite,
        string metric,
        string model,
        double value,
        string threshold,
        bool passed,
        string? reportPath = null)
    {
        var entry = new EvalEntry(suite, metric, model, Math.Round(value, 4), threshold, passed);
        lock (Sync)
        {
            Entries.RemoveAll(e => e.Suite == suite && e.Metric == metric);
            Entries.Add(entry);

            var path = reportPath ?? Environment.GetEnvironmentVariable("JOBFIT_EVAL_REPORT");
            if (!string.IsNullOrWhiteSpace(path))
            {
                WriteTo(path, Entries);
            }
        }
    }

    internal static void WriteTo(string path, IReadOnlyList<EvalEntry> entries)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, FormatMarkdown(entries));
        File.WriteAllText($"{path}.json", JsonSerializer.Serialize(entries, JsonOptions));
    }

    internal static string FormatMarkdown(IReadOnlyList<EvalEntry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Job Fit Copilot — Evaluation Report");
        builder.AppendLine();
        builder.AppendLine("| Suite | Metric | Model | Value | Threshold | Status |");
        builder.AppendLine("|---|---|---|---:|---:|---|");
        foreach (var entry in entries)
        {
            var valueText = entry.Value.ToString("0.00", CultureInfo.InvariantCulture);
            var status = entry.Passed ? "PASS" : "FAIL";
            builder.AppendLine(
                $"| {entry.Suite} | {entry.Metric} | {entry.Model} | {valueText} | {entry.Threshold} | {status} |");
        }

        return builder.ToString();
    }
}
