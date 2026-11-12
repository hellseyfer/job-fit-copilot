using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace JobFit.Api.Tests;

public class RequirementExtractionEndpointTests
{
    private const string Endpoint = "/api/requirements/extract";

    private const string Posting = """
        Senior Backend Engineer at Acme.
        Requirements: 5+ years of C#. Experience with Docker.
        Required: Kubernetes.
        Nice to have: Terraform.
        """;

    private const string ModelOutput = """
        {
          "title": "Senior Backend Engineer",
          "seniority": "senior",
          "requirements": [
            { "description": "5+ years of C#", "category": "experience", "importance": "must-have", "excerpt": "5+ years of C#" },
            { "description": "Docker", "category": "skill", "importance": "must-have", "excerpt": "Experience with Docker" },
            { "description": "Kubernetes", "category": "skill", "importance": "must-have", "excerpt": "Required: Kubernetes" },
            { "description": "Terraform", "category": "skill", "importance": "nice-to-have", "excerpt": "Nice to have: Terraform" }
          ]
        }
        """;

    [Fact]
    public async Task Posting_with_several_requirements_returns_them_with_category_and_excerpt()
    {
        var (response, body) = await ExtractAsync(FakeChatClient.Returning(ModelOutput), Posting);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Senior Backend Engineer", body!["title"]!.GetValue<string>());
        Assert.Equal("senior", body["seniority"]!.GetValue<string>());

        var requirements = body["requirements"]!.AsArray();
        Assert.Equal(4, requirements.Count);
        Assert.Equal("experience", requirements[0]!["category"]!.GetValue<string>());
        Assert.Equal("5+ years of C#", requirements[0]!["excerpt"]!.GetValue<string>());
        Assert.Equal("skill", requirements[1]!["category"]!.GetValue<string>());
        Assert.Equal("Experience with Docker", requirements[1]!["excerpt"]!.GetValue<string>());
        Assert.Equal(4, requirements.Select(r => r!["id"]!.GetValue<string>()).Distinct().Count());
    }

    [Fact]
    public async Task Required_and_optional_requirements_have_their_importance()
    {
        var (_, body) = await ExtractAsync(FakeChatClient.Returning(ModelOutput), Posting);

        var importanceByDescription = body!["requirements"]!.AsArray()
            .ToDictionary(r => r!["description"]!.GetValue<string>(), r => r!["importance"]!.GetValue<string>());
        Assert.Equal("must-have", importanceByDescription["Kubernetes"]);
        Assert.Equal("nice-to-have", importanceByDescription["Terraform"]);
    }

    [Fact]
    public async Task Ungrounded_requirement_is_not_returned()
    {
        const string output = """
            {
              "title": "Senior Backend Engineer",
              "seniority": "senior",
              "requirements": [
                { "description": "C#", "category": "skill", "importance": "must-have", "excerpt": "5+ years of c#" },
                { "description": "Rust", "category": "skill", "importance": "must-have", "excerpt": "Expert in Rust" }
              ]
            }
            """;

        var (response, body) = await ExtractAsync(FakeChatClient.Returning(output), Posting);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var requirement = Assert.Single(body!["requirements"]!.AsArray());
        Assert.Equal("C#", requirement!["description"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Empty_posting_returns_400_without_calling_the_model(string? posting)
    {
        var chatClient = FakeChatClient.Returning(ModelOutput);

        var (response, _) = await ExtractAsync(chatClient, posting);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, chatClient.CallCount);
    }

    [Fact]
    public async Task Oversized_posting_returns_400_without_calling_the_model()
    {
        var chatClient = FakeChatClient.Returning(ModelOutput);

        var (response, _) = await ExtractAsync(chatClient, new string('a', 20_001));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, chatClient.CallCount);
    }

    [Fact]
    public async Task Model_provider_error_returns_502()
    {
        var (response, _) = await ExtractAsync(
            FakeChatClient.Throwing(new HttpRequestException("provider unavailable")), Posting);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("this is not json")]
    [InlineData("""{ "title": "Engineer", "seniority": "senior" }""")]
    [InlineData("""{ "title": "Engineer", "seniority": "galactic", "requirements": [] }""")]
    public async Task Invalid_model_output_returns_502(string output)
    {
        var (response, _) = await ExtractAsync(FakeChatClient.Returning(output), Posting);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Text_without_requirements_returns_an_empty_list()
    {
        const string output = """{ "title": "", "seniority": "unspecified", "requirements": [] }""";

        var (response, body) = await ExtractAsync(FakeChatClient.Returning(output), "The weather is nice today.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(body!["requirements"]!.AsArray());
    }

    [Fact]
    public async Task Model_call_is_traced_with_model_and_token_usage()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Experimental.Microsoft.Extensions.AI",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { lock (activities) { activities.Add(activity); } },
        };
        ActivitySource.AddActivityListener(listener);

        var (response, _) = await ExtractAsync(FakeChatClient.Returning(ModelOutput), Posting);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Activity span;
        lock (activities)
        {
            span = Assert.Single(activities, a => a.GetTagItem("gen_ai.response.model") as string == FakeChatClient.ModelId);
        }
        Assert.Equal(FakeChatClient.ModelId, span.GetTagItem("gen_ai.request.model"));
        Assert.Equal(120, Convert.ToInt32(span.GetTagItem("gen_ai.usage.input_tokens")));
        Assert.Equal(45, Convert.ToInt32(span.GetTagItem("gen_ai.usage.output_tokens")));
    }

    private static async Task<(HttpResponseMessage Response, JsonNode? Body)> ExtractAsync(
        FakeChatClient chatClient, string? posting)
    {
        await using var factory = new JobFitApiFactory(chatClient);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(Endpoint, new { jobPosting = posting });
        var content = await response.Content.ReadAsStringAsync();
        var body = response.IsSuccessStatusCode ? JsonNode.Parse(content) : null;
        return (response, body);
    }
}
