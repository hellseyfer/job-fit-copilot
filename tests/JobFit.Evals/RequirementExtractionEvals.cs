using System.ClientModel;
using System.Text.Json;
using JobFit.Api.Requirements;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using OllamaSharp;
using OpenAI;
using Xunit.Abstractions;

namespace JobFit.Evals;

// Runs the extractor against a real model. Skipped unless OPENAI_API_KEY or OLLAMA_ENDPOINT is set.
public class RequirementExtractionEvals(ITestOutputHelper output)
{
    private const double MinimumMustHaveRecall = 0.75;

    public static TheoryData<string> Cases() => new(LoadCases().Select(c => c.Name));

    [SkippableTheory]
    [MemberData(nameof(Cases))]
    public async Task Extracts_must_have_requirements_from_golden_posting(string caseName)
    {
        using var chatClient = CreateChatClient();
        Skip.If(chatClient is null, "Set OPENAI_API_KEY or OLLAMA_ENDPOINT to run the evals.");

        var golden = LoadCases().Single(c => c.Name == caseName);
        var extractor = new RequirementExtractor(chatClient, NullLogger<RequirementExtractor>.Instance);

        var result = await extractor.ExtractAsync(golden.JobPosting, CancellationToken.None);

        var mustHaveFound = golden.MustHave.Count(keyword => Mentions(result, keyword, RequirementImportance.MustHave));
        var niceToHaveFound = golden.NiceToHave.Count(keyword => Mentions(result, keyword, RequirementImportance.NiceToHave));
        var mustHaveRecall = (double)mustHaveFound / golden.MustHave.Length;

        output.WriteLine($"{caseName}: {result.Requirements.Count} requirements");
        foreach (var requirement in result.Requirements)
        {
            output.WriteLine($"  {requirement.Id} [{requirement.Importance}/{requirement.Category}] {requirement.Description} <- \"{requirement.Excerpt}\"");
        }
        output.WriteLine($"must-have recall: {mustHaveFound}/{golden.MustHave.Length} ({mustHaveRecall:P0})");
        output.WriteLine($"nice-to-have classified: {niceToHaveFound}/{golden.NiceToHave.Length}");

        Assert.True(mustHaveRecall >= MinimumMustHaveRecall,
            $"Must-have recall {mustHaveRecall:P0} is below {MinimumMustHaveRecall:P0}.");
    }

    private static bool Mentions(ExtractRequirementsResponse result, string keyword, RequirementImportance importance) =>
        result.Requirements.Any(r => r.Importance == importance
            && (r.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || r.Excerpt.Contains(keyword, StringComparison.OrdinalIgnoreCase)));

    private static IChatClient? CreateChatClient()
    {
        var model = Environment.GetEnvironmentVariable("JOBFIT_EVAL_MODEL");

        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(openAiKey))
        {
            return new OpenAIClient(new ApiKeyCredential(openAiKey))
                .GetChatClient(string.IsNullOrEmpty(model) ? "gpt-4o-mini" : model)
                .AsIChatClient();
        }

        var ollamaEndpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT");
        if (!string.IsNullOrEmpty(ollamaEndpoint))
        {
            var httpClient = new HttpClient { BaseAddress = new Uri(ollamaEndpoint), Timeout = TimeSpan.FromMinutes(10) };
            return new OllamaApiClient(httpClient, string.IsNullOrEmpty(model) ? "qwen2.5:3b" : model);
        }

        return null;
    }

    private static GoldenCase[] LoadCases() =>
        JsonSerializer.Deserialize<GoldenCase[]>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "requirement-extraction.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private sealed record GoldenCase(string Name, string JobPosting, string[] MustHave, string[] NiceToHave);
}
