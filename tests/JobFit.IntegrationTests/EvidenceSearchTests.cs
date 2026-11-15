using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using JobFit.Api.Tests;

namespace JobFit.IntegrationTests;

[Collection(PgVectorCollection.Name)]
public sealed class EvidenceSearchTests(PgVectorFixture database) : IAsyncLifetime
{
    // One section per topic, so each chunk's vector is a single topic axis.
    private const string Cv = """
        # Databases

        Tuned PostgreSQL queries for the payments platform.

        # Frontend

        Built internal dashboards with React.

        # Platform

        Ran the services on Kubernetes.

        # Data

        Wrote Python batch pipelines.

        # Infrastructure

        Provisioned the cloud accounts with Terraform.
        """;

    private static readonly string[] Topics = ["postgresql", "react", "kubernetes", "python", "terraform"];

    // Each test searches under its own model id, so documents of other tests sharing the database never match.
    private readonly FakeEmbeddingGenerator _embeddings = new()
    {
        Model = $"fake-{Guid.NewGuid():N}",
        VectorFor = TopicVector,
    };

    private KnowledgeApiFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new KnowledgeApiFactory(database.ConnectionString, _embeddings);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Relevant_chunk_ranks_first_and_results_keep_the_request_order()
    {
        await PutAsync(NewId(), "cv", Cv);

        var body = await SearchAsync(new
        {
            requirements = new[]
            {
                new { id = "req-b", description = "React frontends" },
                new { id = "req-a", description = "Experience with PostgreSQL" },
            },
        });

        var results = body["results"]!.AsArray();
        Assert.Equal(["req-b", "req-a"], results.Select(r => r!["requirementId"]!.GetValue<string>()));
        Assert.Contains("React", Evidence(results[0]!)[0]["text"]!.GetValue<string>());
        Assert.Contains("PostgreSQL", Evidence(results[1]!)[0]["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task Evidence_is_ordered_by_score_and_cites_the_stored_chunk()
    {
        var id = NewId();
        await PutAsync(id, "cv", Cv);

        var body = await SearchAsync(new { requirements = new[] { new { id = "req-1", description = "PostgreSQL" } } });

        var evidence = Evidence(body["results"]![0]!);
        var scores = evidence.Select(e => e["score"]!.GetValue<double>()).ToList();
        Assert.Equal(scores.OrderByDescending(s => s), scores);
        Assert.Equal(1, scores[0], precision: 4);

        var best = evidence[0];
        Assert.Equal(id, best["documentId"]!.GetValue<string>());
        Assert.Equal("cv", best["kind"]!.GetValue<string>());
        Assert.Equal("Jane Doe CV", best["title"]!.GetValue<string>());
        Assert.Null(best["embedding"]);
        var chunks = (await _client.GetFromJsonAsync<JsonNode>($"/api/knowledge/documents/{id}/chunks"))!["chunks"]!.AsArray();
        var stored = Assert.Single(chunks, c => c!["index"]!.GetValue<int>() == best["chunkIndex"]!.GetValue<int>())!;
        Assert.Equal(stored["section"]!.GetValue<string>(), best["section"]!.GetValue<string>());
        Assert.Equal(stored["text"]!.GetValue<string>(), best["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task Top_defaults_to_three_and_can_be_lowered()
    {
        await PutAsync(NewId(), "cv", Cv);
        var requirements = new[] { new { id = "req-1", description = "PostgreSQL" } };

        var byDefault = await SearchAsync(new { requirements });
        var single = await SearchAsync(new { requirements, top = 1 });

        Assert.Equal(3, Evidence(byDefault["results"]![0]!).Count);
        Assert.Single(Evidence(single["results"]![0]!));
    }

    [Fact]
    public async Task Kind_filter_returns_only_that_kind()
    {
        await PutAsync(NewId(), "cv", Cv);
        await PutAsync(NewId(), "project", "# Migration\n\nMoved PostgreSQL to a managed service.");

        var body = await SearchAsync(new
        {
            requirements = new[] { new { id = "req-1", description = "PostgreSQL" } },
            kind = "project",
        });

        var evidence = Evidence(body["results"]![0]!);
        Assert.NotEmpty(evidence);
        Assert.All(evidence, e => Assert.Equal("project", e["kind"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Chunks_of_another_embedding_model_are_ignored()
    {
        await PutAsync(NewId(), "cv", Cv);
        _embeddings.Model = $"fake-{Guid.NewGuid():N}";

        var body = await SearchAsync(new { requirements = new[] { new { id = "req-1", description = "PostgreSQL" } } });

        Assert.Equal(_embeddings.Model, body["embeddingModel"]!.GetValue<string>());
        Assert.Empty(Evidence(body["results"]![0]!));
    }

    [Fact]
    public async Task Empty_store_returns_empty_evidence()
    {
        var body = await SearchAsync(new
        {
            requirements = new[] { new { id = "req-1", description = "PostgreSQL" }, new { id = "req-2", description = "React" } },
        });

        var results = body["results"]!.AsArray();
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Empty(Evidence(r!)));
    }

    [Fact]
    public async Task Provider_error_returns_502()
    {
        _embeddings.Error = new HttpRequestException("provider down");

        using var response = await PostAsync(new { requirements = new[] { new { id = "req-1", description = "PostgreSQL" } } });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_embedding_dimensions_return_502()
    {
        _embeddings.Dimensions = 10;

        using var response = await PostAsync(new { requirements = new[] { new { id = "req-1", description = "PostgreSQL" } } });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Requirements_are_embedded_in_one_traced_call()
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
        var callsBefore = _embeddings.CallCount;

        await SearchAsync(new
        {
            requirements = new[]
            {
                new { id = "req-1", description = "PostgreSQL" },
                new { id = "req-2", description = "React" },
                new { id = "req-3", description = "Kubernetes" },
            },
        });

        Assert.Equal(callsBefore + 1, _embeddings.CallCount);
        Activity span;
        lock (activities)
        {
            span = Assert.Single(activities, a => a.GetTagItem("gen_ai.request.model") as string == _embeddings.Model);
        }
        Assert.True(Convert.ToInt32(span.GetTagItem("gen_ai.usage.input_tokens")) > 0);
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

        // A small shared component keeps topic-less inputs non-zero.
        vector[^1] = 0.01f;
        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        return vector.Select(v => v / norm).ToArray();
    }

    private static string NewId() => $"doc-{Guid.NewGuid():N}"[..20];

    private static List<JsonNode> Evidence(JsonNode result) =>
        result["evidence"]!.AsArray().Select(e => e!).ToList();

    private async Task PutAsync(string id, string kind, string content)
    {
        using var response = await _client.PutAsJsonAsync(
            $"/api/knowledge/documents/{id}", new { kind, title = "Jane Doe CV", content });
        response.EnsureSuccessStatusCode();
    }

    private Task<HttpResponseMessage> PostAsync(object body) => _client.PostAsJsonAsync("/api/evidence/search", body);

    private async Task<JsonNode> SearchAsync(object body)
    {
        using var response = await PostAsync(body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonNode>())!;
    }
}
