using System.Net;
using System.Net.Http.Json;
using JobFit.Api.Evidence;
using JobFit.Api.Fit;
using JobFit.Api.Requirements;

namespace JobFit.Api.Tests;

public class FitScorerTests
{
    [Fact]
    public void Score_weights_must_haves_double()
    {
        RequirementAssessment[] assessments =
        [
            Assessment("req-1", RequirementImportance.MustHave, FitStatus.Met),
            Assessment("req-2", RequirementImportance.MustHave, FitStatus.Partial),
            Assessment("req-3", RequirementImportance.NiceToHave, FitStatus.Missing),
        ];

        Assert.Equal(60, FitScorer.Score(assessments));
    }

    [Fact]
    public void Score_is_zero_without_requirements() => Assert.Equal(0, FitScorer.Score([]));

    private static RequirementAssessment Assessment(string id, RequirementImportance importance, FitStatus status) =>
        new(id, "description", importance, status, "rationale", []);
}

public class FitGroundingTests
{
    private static readonly FitRequirement Requirement = new("req-1", "PostgreSQL", RequirementImportance.MustHave);

    private static readonly EvidenceItem[] Evidence =
        [Item(0, "Tuned PostgreSQL."), Item(1, "Built React apps."), Item(2, "Ran Kubernetes.")];

    [Fact]
    public void Citations_outside_the_provided_items_are_dropped()
    {
        var assessment = FitJudge.Ground(Requirement, new JudgedRequirement("Shown.", [1, 9, 1], FitStatus.Met), Evidence);

        Assert.Equal(FitStatus.Met, assessment.Status);
        Assert.Equal(Evidence[0], Assert.Single(assessment.Evidence));
    }

    [Fact]
    public void Positive_status_without_valid_citations_becomes_missing()
    {
        var assessment = FitJudge.Ground(Requirement, new JudgedRequirement("Shown.", [9], FitStatus.Met), Evidence);

        Assert.Equal(FitStatus.Missing, assessment.Status);
        Assert.Empty(assessment.Evidence);
    }

    [Fact]
    public void Missing_status_cites_nothing()
    {
        var assessment = FitJudge.Ground(Requirement, new JudgedRequirement("Not shown.", [2], FitStatus.Missing), Evidence);

        Assert.Equal(FitStatus.Missing, assessment.Status);
        Assert.Empty(assessment.Evidence);
    }

    [Theory]
    [InlineData("""{"rationale":"Shown.","evidence":[1]}""")]
    [InlineData("""{"rationale":"Shown.","evidence":[1],"status":7}""")]
    public async Task Judgment_without_a_valid_status_fails(string json)
    {
        var judge = new FitJudge(FakeChatClient.Returning(json));

        await Assert.ThrowsAsync<JudgeFailedException>(() => judge.JudgeAsync(Requirement, Evidence, CancellationToken.None));
    }

    private static EvidenceItem Item(int index, string text) => new("cv", "cv", "CV", "Experience", index, text, 0.9);
}

// Invalid requests are rejected before the database or any model is used.
public class FitValidationTests
{
    [Fact]
    public async Task Invalid_importance_returns_400_without_model_calls()
    {
        await AssertRejectedAsync(new { requirements = new[] { new { id = "req-1", description = "C#", importance = "optional" } } });
    }

    [Fact]
    public async Task Missing_importance_returns_400_without_model_calls()
    {
        await AssertRejectedAsync(new { requirements = new[] { new { id = "req-1", description = "C#" } } });
    }

    [Fact]
    public async Task Empty_requirements_return_400_without_model_calls()
    {
        await AssertRejectedAsync(new { requirements = Array.Empty<object>() });
    }

    [Fact]
    public async Task Duplicate_ids_return_400_without_model_calls()
    {
        await AssertRejectedAsync(new
        {
            requirements = new[]
            {
                new { id = "req-1", description = "C#", importance = "must-have" },
                new { id = "req-1", description = "SQL", importance = "must-have" },
            },
        });
    }

    [Fact]
    public async Task Empty_description_returns_400_without_model_calls()
    {
        await AssertRejectedAsync(new { requirements = new[] { new { id = "req-1", description = " ", importance = "must-have" } } });
    }

    [Fact]
    public async Task Invalid_kind_returns_400_without_model_calls()
    {
        await AssertRejectedAsync(new
        {
            requirements = new[] { new { id = "req-1", description = "C#", importance = "must-have" } },
            kind = "letter",
        });
    }

    [Fact]
    public async Task Embedding_error_returns_502_without_judging()
    {
        var chat = FakeChatClient.Returning("""{"status":"met","rationale":"x","evidence":[1]}""");
        var embeddings = new FakeEmbeddingGenerator { Error = new HttpRequestException("provider down") };
        await using var factory = new JobFitApiFactory(chat, embeddings);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/fit/score", new
        {
            requirements = new[] { new { id = "req-1", description = "C#", importance = "must-have" } },
        });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(0, chat.CallCount);
    }

    private static async Task AssertRejectedAsync(object body)
    {
        var chat = FakeChatClient.Returning("{}");
        var embeddings = new FakeEmbeddingGenerator();
        await using var factory = new JobFitApiFactory(chat, embeddings);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/fit/score", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, chat.CallCount);
        Assert.Equal(0, embeddings.CallCount);
    }
}
