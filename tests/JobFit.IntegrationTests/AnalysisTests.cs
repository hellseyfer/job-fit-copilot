using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using JobFit.Api.Tests;
using Microsoft.Extensions.AI;

namespace JobFit.IntegrationTests;

[Collection(PgVectorCollection.Name)]
public sealed class AnalysisTests(PgVectorFixture database) : IAsyncLifetime
{
    private const string Posting = "Backend engineer. Requirements: PostgreSQL, Kubernetes, Rust.";

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

    private readonly FakeEmbeddingGenerator _embeddings = new() { Model = $"fake-{Guid.NewGuid():N}", VectorFor = TopicVector };

    private KnowledgeApiFactory _factory = null!;
    private HttpClient _client = null!;
    private int _judgeCalls;
    private int _plannerCalls;
    private int _writerCalls;
    private int _verifierCalls;
    private readonly ConcurrentQueue<string> _writerPrompts = new();

    // PostgreSQL must-have (met), Kubernetes nice-to-have (met), Rust must-have (missing): score 60.
    private string _extraction = Extraction(("PostgreSQL", "must-have"), ("Kubernetes", "nice-to-have"), ("Rust", "must-have"));
    private Func<string>? _judge;
    private Func<string, string>? _planner;
    private Func<string>? _writer;
    private Func<string, string>? _verifier;

    private const string Letter = """
        {"sentences":[
          {"text":"I am applying for the backend role.","kind":"connective","evidence":[]},
          {"text":"I tuned PostgreSQL queries for a payments platform.","kind":"claim","evidence":[1]},
          {"text":"I wrote Rust in production.","kind":"claim","evidence":[2]}
        ]}
        """;

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
    public async Task Strong_fit_letter_accepted_first_time()
    {
        await PutAsync(NewId(), "cv", Cv);
        _extraction = Extraction(("PostgreSQL", "must-have"), ("Kubernetes", "nice-to-have"));

        var body = await AnalyzeAsync(new { jobPosting = Posting });

        Assert.Equal("strong-fit", body["route"]!.GetValue<string>());
        Assert.Equal(["extract-requirements", "score-fit", "write-cover-letter", "verify-cover-letter", "finalize-cover-letter"], Path(body));
        Assert.Equal(100, body["fit"]!["score"]!.GetValue<int>());
        Assert.False(body.AsObject().ContainsKey("studyPlan"));
        Assert.Equal(0, _plannerCalls);
        Assert.Equal(2, body["requirements"]!.AsArray().Count);

        var letter = body["coverLetter"]!;
        Assert.Equal(1, letter["attempts"]!.GetValue<int>());
        Assert.Empty(letter["removed"]!.AsArray());
        Assert.Equal(3, letter["sentences"]!.AsArray().Count);
        Assert.StartsWith("I am applying for the backend role. I tuned PostgreSQL", letter["text"]!.GetValue<string>());
        var cited = Assert.Single(letter["sentences"]![1]!["evidence"]!.AsArray());
        Assert.Contains("PostgreSQL", cited!["text"]!.GetValue<string>());
        Assert.Equal(1, _writerCalls);
        Assert.Equal(3, _verifierCalls);
    }

    [Fact]
    public async Task Rewrite_fixes_the_letter()
    {
        await PutAsync(NewId(), "cv", Cv);
        _extraction = Extraction(("PostgreSQL", "must-have"), ("Kubernetes", "nice-to-have"));
        _verifier = prompt => _writerCalls == 1 ? Check(prompt, rejectRust: true) : Check(prompt);

        var body = await AnalyzeAsync(new { jobPosting = Posting });

        Assert.Equal(
            ["extract-requirements", "score-fit", "write-cover-letter", "verify-cover-letter", "write-cover-letter", "verify-cover-letter", "finalize-cover-letter"],
            Path(body));
        Assert.Equal(2, body["coverLetter"]!["attempts"]!.GetValue<int>());
        Assert.Empty(body["coverLetter"]!["removed"]!.AsArray());
        Assert.Equal(2, _writerCalls);
        var rewritePrompt = _writerPrompts.Last();
        Assert.Contains("rejected: Rust is not in the evidence.\">I wrote Rust in production.", rewritePrompt);
    }

    [Fact]
    public async Task Sentence_still_unsupported_after_three_drafts_is_removed()
    {
        await PutAsync(NewId(), "cv", Cv);
        _extraction = Extraction(("PostgreSQL", "must-have"), ("Kubernetes", "nice-to-have"));
        _verifier = prompt => Check(prompt, rejectRust: true);

        var body = await AnalyzeAsync(new { jobPosting = Posting });

        var letter = body["coverLetter"]!;
        Assert.Equal(3, letter["attempts"]!.GetValue<int>());
        Assert.Equal(3, _writerCalls);
        var removed = Assert.Single(letter["removed"]!.AsArray());
        Assert.Equal("I wrote Rust in production.", removed!["text"]!.GetValue<string>());
        Assert.Equal("Rust is not in the evidence.", removed["reason"]!.GetValue<string>());
        Assert.DoesNotContain("Rust", letter["text"]!.GetValue<string>());
        Assert.Equal(2, letter["sentences"]!.AsArray().Count);
    }

    [Theory]
    [InlineData("writer", true)]
    [InlineData("writer", false)]
    [InlineData("verifier", true)]
    [InlineData("verifier", false)]
    public async Task Cover_letter_failure_returns_502(string role, bool throws)
    {
        await PutAsync(NewId(), "cv", Cv);
        _extraction = Extraction(("PostgreSQL", "must-have"));
        Func<string> fail = () => throws ? throw new HttpRequestException("down") : "Here is your letter.";
        if (role == "writer")
        {
            _writer = fail;
        }
        else
        {
            _verifier = _ => fail();
        }

        using var response = await PostAsync(new { jobPosting = Posting });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Weak_fit_returns_a_grounded_study_plan()
    {
        await PutAsync(NewId(), "cv", Cv);

        var body = await AnalyzeAsync(new { jobPosting = Posting });

        Assert.Equal("weak-fit", body["route"]!.GetValue<string>());
        Assert.Equal(["extract-requirements", "score-fit", "plan-study"], Path(body));
        Assert.Equal(60, body["fit"]!["score"]!.GetValue<int>());
        var item = Assert.Single(body["studyPlan"]!.AsArray());
        Assert.Equal("req-3", item!["requirementId"]!.GetValue<string>());
        Assert.Equal("Learn it.", item["focus"]!.GetValue<string>());
        Assert.Equal(1, _plannerCalls);
        Assert.False(body.AsObject().ContainsKey("coverLetter"));
        Assert.Equal(0, _writerCalls);
        Assert.Equal(0, _verifierCalls);
    }

    [Theory]
    [InlineData("50")]
    [InlineData("60")]
    public async Task Configured_threshold_at_or_below_the_score_is_a_strong_fit(string threshold)
    {
        CreateClient(new Dictionary<string, string> { ["Workflow:FitThreshold"] = threshold });
        await PutAsync(NewId(), "cv", Cv);

        var body = await AnalyzeAsync(new { jobPosting = Posting });

        Assert.Equal(60, body["fit"]!["score"]!.GetValue<int>());
        Assert.Equal("strong-fit", body["route"]!.GetValue<string>());
    }

    [Fact]
    public async Task Kind_restricts_the_cited_evidence()
    {
        await PutAsync(NewId(), "cv", Cv);
        var projectId = NewId();
        await PutAsync(projectId, "project", Project);

        var body = await AnalyzeAsync(new { jobPosting = Posting, kind = "project" });

        var cited = body["fit"]!["assessments"]!.AsArray().SelectMany(a => a!["evidence"]!.AsArray()).ToList();
        Assert.NotEmpty(cited);
        Assert.All(cited, e => Assert.Equal(projectId, e!["documentId"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Weak_fit_without_gaps_has_an_empty_plan_and_no_planner_call()
    {
        CreateClient(new Dictionary<string, string> { ["Workflow:FitThreshold"] = "101" });
        await PutAsync(NewId(), "cv", Cv);
        _extraction = Extraction(("PostgreSQL", "must-have"));

        var body = await AnalyzeAsync(new { jobPosting = Posting });

        Assert.Equal("weak-fit", body["route"]!.GetValue<string>());
        Assert.Empty(body["studyPlan"]!.AsArray());
        Assert.Equal(0, _plannerCalls);
    }

    [Fact]
    public async Task Judge_failure_returns_502()
    {
        await PutAsync(NewId(), "cv", Cv);
        _judge = () => throw new HttpRequestException("down");

        using var response = await PostAsync(new { jobPosting = Posting });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Study_plan_failure_returns_502(bool throws)
    {
        await PutAsync(NewId(), "cv", Cv);
        _planner = _ => throws ? throw new HttpRequestException("down") : "I would study Rust.";

        using var response = await PostAsync(new { jobPosting = Posting });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Run_and_nodes_are_traced_with_model_calls_in_the_same_trace()
    {
        await PutAsync(NewId(), "cv", Cv);
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is "Microsoft.Agents.AI.Workflows" or "Experimental.Microsoft.Extensions.AI",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => { lock (activities) { activities.Add(activity); } },
        };
        ActivitySource.AddActivityListener(listener);

        await AnalyzeAsync(new { jobPosting = Posting });

        List<Activity> workflow, chat;
        lock (activities)
        {
            workflow = activities.Where(a => a.Source.Name == "Microsoft.Agents.AI.Workflows").ToList();
            chat = activities.Where(a => a.GetTagItem("gen_ai.operation.name") as string == "chat").ToList();
        }

        var run = Assert.Single(workflow, a => a.GetTagItem("gen_ai.operation.name") as string == "invoke_workflow");
        foreach (var executor in new[] { "extract-requirements", "score-fit", "plan-study" })
        {
            Assert.Contains(workflow, a => a.TraceId == run.TraceId && a.GetTagItem("executor.id") as string == executor);
        }

        Assert.Equal(5, chat.Count);
        Assert.All(chat, c => Assert.Equal(run.TraceId, c.TraceId));
    }

    // Supports the checked sentence unless it is about Rust and Rust is rejected.
    private static string Check(string prompt, bool rejectRust = false) =>
        rejectRust && prompt.Contains("Rust", StringComparison.Ordinal)
            ? """{"quote":"","reason":"Rust is not in the evidence.","verdict":"unsupported"}"""
            : """{"quote":"PostgreSQL","reason":"Shown.","verdict":"supported"}""";

    private static string Extraction(params (string Skill, string Importance)[] requirements)
    {
        var items = string.Join(",", requirements.Select(r =>
            $$"""{"description":"{{r.Skill}}","category":"skill","importance":"{{r.Importance}}","excerpt":"{{r.Skill}}"}"""));
        return $$"""{"title":"Backend engineer","seniority":"mid","requirements":[{{items}}]}""";
    }

    // One fake model plays the three roles; the system prompt tells which one is being called.
    private string Respond(IEnumerable<ChatMessage> messages)
    {
        var system = messages.First().Text;
        var prompt = messages.Last().Text;
        if (system.StartsWith("You judge", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _judgeCalls);
            if (_judge is not null)
            {
                return _judge();
            }

            var requirement = prompt[..prompt.IndexOf("</requirement>", StringComparison.Ordinal)];
            return requirement.Contains("Rust", StringComparison.Ordinal)
                ? """{"rationale":"No Rust in the evidence.","evidence":[],"status":"missing"}"""
                : """{"rationale":"The evidence shows it.","evidence":[1],"status":"met"}""";
        }

        if (system.StartsWith("You write a short cover letter", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _writerCalls);
            _writerPrompts.Enqueue(prompt);
            return _writer?.Invoke() ?? Letter;
        }

        if (system.StartsWith("You check one sentence of a cover letter", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _verifierCalls);
            return _verifier?.Invoke(prompt) ?? Check(prompt);
        }

        if (system.StartsWith("You write a study plan", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _plannerCalls);
            return _planner?.Invoke(prompt) ?? """
                {"items":[
                  {"requirementId":"req-3","focus":"Learn it.","steps":["Build a CLI in Rust."]},
                  {"requirementId":"req-1","focus":"Already met.","steps":["Nothing."]},
                  {"requirementId":"req-9","focus":"Unknown.","steps":["Nothing."]}
                ]}
                """;
        }

        return _extraction;
    }

    private void CreateClient(IReadOnlyDictionary<string, string>? settings = null)
    {
        _client?.Dispose();
        _factory?.Dispose();
        _factory = new KnowledgeApiFactory(database.ConnectionString, _embeddings, FakeChatClient.Responding(Respond), settings);
        _client = _factory.CreateClient();
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

    private static IEnumerable<string> Path(JsonNode body) => body["path"]!.AsArray().Select(p => p!.GetValue<string>());

    private async Task PutAsync(string id, string kind, string content)
    {
        using var response = await _client.PutAsJsonAsync(
            $"/api/knowledge/documents/{id}", new { kind, title = "Jane Doe", content });
        response.EnsureSuccessStatusCode();
    }

    private Task<HttpResponseMessage> PostAsync(object body) => _client.PostAsJsonAsync("/api/analyses", body);

    private async Task<JsonNode> AnalyzeAsync(object body)
    {
        using var response = await PostAsync(body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonNode>())!;
    }
}
