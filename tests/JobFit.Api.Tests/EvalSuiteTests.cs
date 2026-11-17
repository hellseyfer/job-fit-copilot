using System.Text.Json;
using JobFit.Evals;
using Xunit.Sdk;

namespace JobFit.Api.Tests;

public class EvalSuiteTests
{
    [Fact]
    public void RequireChatClient_fails_when_JOBFIT_EVAL_REQUIRED_is_true_and_no_model_is_configured()
    {
        static string? GetEnv(string name) => name == "JOBFIT_EVAL_REQUIRED" ? "true" : null;

        var exception = Assert.Throws<FailException>(() => EvalHarness.RequireChatClient(GetEnv));
        Assert.Contains("JOBFIT_EVAL_REQUIRED=true", exception.Message);
    }

    [Fact]
    public void RequireEmbeddingGenerator_fails_when_JOBFIT_EVAL_REQUIRED_is_true_and_no_model_is_configured()
    {
        static string? GetEnv(string name) => name == "JOBFIT_EVAL_REQUIRED" ? "true" : null;

        var exception = Assert.Throws<FailException>(() => EvalHarness.RequireEmbeddingGenerator(GetEnv));
        Assert.Contains("JOBFIT_EVAL_REQUIRED=true", exception.Message);
    }

    [Fact]
    public void RequireChatClient_skips_when_JOBFIT_EVAL_REQUIRED_is_not_set_and_no_model_is_configured()
    {
        static string? GetEnv(string _) => null;

        Assert.Throws<Xunit.SkipException>(() => EvalHarness.RequireChatClient(GetEnv));
    }

    [Fact]
    public void RequireEmbeddingGenerator_skips_when_JOBFIT_EVAL_REQUIRED_is_not_set_and_no_model_is_configured()
    {
        static string? GetEnv(string _) => null;

        Assert.Throws<Xunit.SkipException>(() => EvalHarness.RequireEmbeddingGenerator(GetEnv));
    }

    [Fact]
    public void EvalReport_writes_markdown_table_and_companion_json_file()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"jobfit-eval-report-{Guid.NewGuid():N}");
        var reportPath = Path.Combine(tempDir, "summary.md");
        try
        {
            EvalReport.Record("requirement-extraction", "must-have recall", "qwen2.5:3b", 0.85, ">= 0.75", passed: true, reportPath);
            EvalReport.Record("input-guardrails", "false-positive rate", "qwen2.5:3b", 0.08, "<= 0.10", passed: true, reportPath);

            Assert.True(File.Exists(reportPath));
            Assert.True(File.Exists($"{reportPath}.json"));

            var markdown = File.ReadAllText(reportPath);
            Assert.Contains("| requirement-extraction | must-have recall | qwen2.5:3b | 0.85 | >= 0.75 | PASS |", markdown);
            Assert.Contains("| input-guardrails | false-positive rate | qwen2.5:3b | 0.08 | <= 0.10 | PASS |", markdown);

            var entries = JsonSerializer.Deserialize<List<EvalEntry>>(
                File.ReadAllText($"{reportPath}.json"),
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            Assert.Contains(entries, e => e is { Suite: "requirement-extraction", Metric: "must-have recall", Value: 0.85, Passed: true });
            Assert.Contains(entries, e => e is { Suite: "input-guardrails", Metric: "false-positive rate", Value: 0.08, Passed: true });
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void EndToEnd_golden_dataset_contains_strong_fit_weak_fit_and_prompt_injection_cases()
    {
        var path = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "JobFit.Evals", "golden", "end-to-end.json"));
        Assert.True(File.Exists(path), $"Expected golden file at {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        Assert.True(root.GetProperty("project").GetProperty("content").GetString()!.Length > 0);
        Assert.NotEmpty(root.GetProperty("absentFromKnowledge").EnumerateArray());

        var outcomes = root.GetProperty("cases")
            .EnumerateArray()
            .Select(c => c.GetProperty("expectedOutcome").GetString())
            .ToHashSet();
        Assert.Contains("strong-fit", outcomes);
        Assert.Contains("weak-fit", outcomes);
        Assert.Contains("prompt-injection", outcomes);
    }

    [Fact]
    public void Evals_workflow_defines_the_manual_evals_quality_gate_job()
    {
        var path = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".github", "workflows", "evals.yml"));
        Assert.True(File.Exists(path), $"Expected Evals workflow at {path}");

        var yaml = File.ReadAllText(path);
        Assert.Contains("workflow_dispatch:", yaml);
        Assert.DoesNotContain("pull_request:", yaml);
        Assert.Contains("evals:", yaml);
        Assert.Contains("dotnet test tests/JobFit.Evals", yaml);
        Assert.Contains("JOBFIT_EVAL_REQUIRED: \"true\"", yaml);
        Assert.Contains("JOBFIT_EVAL_REPORT=", yaml);
    }
}
