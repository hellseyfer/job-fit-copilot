using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using JobFit.Api.Tests;
using Microsoft.Extensions.AI;

namespace JobFit.IntegrationTests;

[Collection(PgVectorCollection.Name)]
public sealed class FitScoringTests(PgVectorFixture database) : IAsyncLifetime
{
    private const string Cv = """
        # Databases

        Tuned PostgreSQL queries for the payments platform.

        # Platform

        Ran the services on Kubernetes.
        """;

    private const string Project = """
        # Homelab

        Runs a Kubernetes cluster with GitOps.
        """;

    private static readonly string[] Topics = ["postgresql", "kubernetes", "rust"];

    private readonly FakeEmbeddingGenerator _embeddings = new()
    {
        Model = $"fake-{Guid.NewGuid():N}",
        VectorFor = TopicVector,
    };

    private KnowledgeApiFactory _factory = null!;
    private HttpClient _client = null!;

    // Judges "met" citing evidence 1 when the requirement is one the CV has a section for.
    private FakeChatClient _chat = FakeChatClient.Responding(messages =>
    {
        var prompt = messages.Last().Text;
        var requirement = prompt[..prompt.IndexOf("</requirement>", StringComparison.Ordinal)];
        return requirement.Contains("Rust", StringComparison.Ordinal)
            ? """{"status":"missing","rationale":"No Rust in the evidence.","evidence":[]}"""
            : """{"status":"met","rationale":"The evidence shows it.","evidence":[1]}""";
    });

    public Task InitializeAsync()
    {
        CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Supported_requirement_is_met_with_citation_and_unsupported_is_missing()
    {
        var documentId = NewId();
        await PutAsync(documentId, "cv", Cv);

        var body = await ScoreAsync(new
        {
            requirements = new object[]
            {
                new { id = "req-rust", description = "Rust", importance = "nice-to-have", category = "skill", excerpt = "Rust" },
                new { id = "req-pg", description = "PostgreSQL", importance = "must-have", category = "skill", excerpt = "PostgreSQL" },
            },
        });

        var assessments = body["assessments"]!.AsArray();
        Assert.Equal(["req-rust", "req-pg"], assessments.Select(a => a!["requirementId"]!.GetValue<string>()));
        Assert.Equal("missing", assessments[0]!["status"]!.GetValue<string>());
        Assert.Empty(assessments[0]!["evidence"]!.AsArray());
        Assert.Equal("met", assessments[1]!["status"]!.GetValue<string>());
        var cited = Assert.Single(assessments[1]!["evidence"]!.AsArray())!;
        Assert.Equal(documentId, cited["documentId"]!.GetValue<string>());
        Assert.Equal("Databases", cited["section"]!.GetValue<string>());
        Assert.Contains("PostgreSQL", cited["text"]!.GetValue<string>());
        Assert.Equal(67, body["score"]!.GetValue<int>());
        Assert.Equal(["req-pg"], body["strengths"]!.AsArray().Select(s => s!.GetValue<string>()));
        Assert.Equal(["req-rust"], body["gaps"]!.AsArray().Select(s => s!.GetValue<string>()));
    }

    [Fact]
    public async Task Kind_filter_cites_only_that_kind()
    {
        await PutAsync(NewId(), "cv", Cv);
        await PutAsync(NewId(), "project", Project);

        var body = await ScoreAsync(new
        {
            requirements = new[] { new { id = "req-1", description = "Kubernetes", importance = "must-have" } },
            kind = "project",
        });

        var evidence = body["assessments"]![0]!["evidence"]!.AsArray();
        Assert.NotEmpty(evidence);
        Assert.All(evidence, e => Assert.Equal("project", e!["kind"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Each_requirement_with_evidence_is_judged_once()
    {
        await PutAsync(NewId(), "cv", Cv);

        await ScoreAsync(new
        {
            requirements = new[]
            {
                new { id = "req-1", description = "PostgreSQL", importance = "must-have" },
                new { id = "req-2", description = "Kubernetes", importance = "must-have" },
                new { id = "req-3", description = "Rust", importance = "nice-to-have" },
            },
        });

        Assert.Equal(3, _chat.CallCount);
    }

    [Fact]
    public async Task Empty_store_gives_missing_without_judging()
    {
        var body = await ScoreAsync(new { requirements = new[] { new { id = "req-1", description = "PostgreSQL", importance = "must-have" } } });

        Assert.Equal("missing", body["assessments"]![0]!["status"]!.GetValue<string>());
        Assert.Equal(0, body["score"]!.GetValue<int>());
        Assert.Equal(0, _chat.CallCount);
    }

    [Fact]
    public async Task Chat_model_error_returns_502()
    {
        await PutAsync(NewId(), "cv", Cv);
        ReplaceChat(FakeChatClient.Throwing(new HttpRequestException("provider down")));

        using var response = await PostAsync(new { requirements = new[] { new { id = "req-1", description = "PostgreSQL", importance = "must-have" } } });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Invalid_judge_output_returns_502()
    {
        await PutAsync(NewId(), "cv", Cv);
        ReplaceChat(FakeChatClient.Returning("I think the candidate is great."));

        using var response = await PostAsync(new { requirements = new[] { new { id = "req-1", description = "PostgreSQL", importance = "must-have" } } });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Judge_call_is_traced_with_model_and_tokens()
    {
        await PutAsync(NewId(), "cv", Cv);
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Experimental.Microsoft.Extensions.AI",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { lock (activities) { activities.Add(activity); } },
        };
        ActivitySource.AddActivityListener(listener);

        await ScoreAsync(new { requirements = new[] { new { id = "req-1", description = "PostgreSQL", importance = "must-have" } } });

        Activity span;
        lock (activities)
        {
            span = Assert.Single(activities, a => a.GetTagItem("gen_ai.response.model") as string == FakeChatClient.ModelId
                && a.GetTagItem("gen_ai.operation.name") as string == "chat");
        }
        Assert.Equal(120, Convert.ToInt32(span.GetTagItem("gen_ai.usage.input_tokens")));
        Assert.Equal(45, Convert.ToInt32(span.GetTagItem("gen_ai.usage.output_tokens")));
    }

    private void CreateClient()
    {
        _factory = new KnowledgeApiFactory(database.ConnectionString, _embeddings, _chat);
        _client = _factory.CreateClient();
    }

    private void ReplaceChat(FakeChatClient chat)
    {
        _client.Dispose();
        _factory.Dispose();
        _chat = chat;
        CreateClient();
    }

    private static float[] TopicVector(string input, int dimensions)
    {
        var vector = new float[dimensions];
        for (var i = 0; i < Topics.Length; i++)
        {
            if (input.Contains(Topics[i], StringComparison.OrdinalIgnoreCase))
            {
                vector[i] = 1;
            }
        }

        vector[^1] = 0.01f;
        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        return vector.Select(v => v / norm).ToArray();
    }

    private static string NewId() => $"doc-{Guid.NewGuid():N}"[..20];

    private async Task PutAsync(string id, string kind, string content)
    {
        using var response = await _client.PutAsJsonAsync(
            $"/api/knowledge/documents/{id}", new { kind, title = "Jane Doe", content });
        response.EnsureSuccessStatusCode();
    }

    private Task<HttpResponseMessage> PostAsync(object body) => _client.PostAsJsonAsync("/api/fit/score", body);

    private async Task<JsonNode> ScoreAsync(object body)
    {
        using var response = await PostAsync(body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonNode>())!;
    }
}
