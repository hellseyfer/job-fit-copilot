using System.Net;
using System.Net.Http.Json;
using JobFit.Api.Analysis;
using JobFit.Api.Fit;
using JobFit.Api.Requirements;

namespace JobFit.Api.Tests;

public class AnalysisEndpointTests
{
    [Theory]
    [InlineData("", null)]
    [InlineData("   ", "cv")]
    [InlineData("Backend engineer with C#.", "resume")]
    public async Task Invalid_input_returns_400_without_model_calls(string jobPosting, string? kind)
    {
        var chat = FakeChatClient.Returning("{}");
        var embeddings = new FakeEmbeddingGenerator();
        await using var factory = new JobFitApiFactory(chat, embeddings);

        using var response = await factory.CreateClient().PostAsJsonAsync("/api/analyses", new { jobPosting, kind });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, chat.CallCount);
        Assert.Equal(0, embeddings.CallCount);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(false, 2001)]
    public async Task Invalid_approval_returns_400_without_touching_the_run(bool? approved, int? feedbackLength)
    {
        var chat = FakeChatClient.Returning("{}");
        await using var factory = new JobFitApiFactory(chat);
        var feedback = feedbackLength is { } length ? new string('a', length) : null;

        using var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/analyses/run-1/approval", new { approved, feedback });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, chat.CallCount);
    }

    [Fact]
    public async Task Too_long_posting_returns_400()
    {
        var chat = FakeChatClient.Returning("{}");
        await using var factory = new JobFitApiFactory(chat);

        using var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/analyses", new { jobPosting = new string('a', RequirementEndpoints.MaxJobPostingLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, chat.CallCount);
    }

    [Fact]
    public async Task Extraction_failure_returns_502_without_embedding()
    {
        var embeddings = new FakeEmbeddingGenerator();
        await using var factory = new JobFitApiFactory(FakeChatClient.Throwing(new HttpRequestException("down")), embeddings);

        using var response = await factory.CreateClient().PostAsJsonAsync("/api/analyses", new { jobPosting = "Needs C#." });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(0, embeddings.CallCount);
    }

    [Fact]
    public async Task Invalid_extraction_output_returns_502()
    {
        await using var factory = new JobFitApiFactory(FakeChatClient.Returning("not json"), new FakeEmbeddingGenerator());

        using var response = await factory.CreateClient().PostAsJsonAsync("/api/analyses", new { jobPosting = "Needs C#." });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task Unscorable_requirement_count_returns_422_without_embedding(int count)
    {
        var skills = Enumerable.Range(1, count).Select(i => $"skill-{i:D2}").ToList();
        var posting = $"Requirements: {string.Join(", ", skills)}.";
        var requirements = string.Join(",", skills.Select(s =>
            $$"""{"description":"{{s}}","category":"skill","importance":"must-have","excerpt":"{{s}}"}"""));
        var output = $$"""{"title":"Engineer","seniority":"mid","requirements":[{{requirements}}]}""";
        var embeddings = new FakeEmbeddingGenerator();
        await using var factory = new JobFitApiFactory(FakeChatClient.Returning(output), embeddings);

        using var response = await factory.CreateClient().PostAsJsonAsync("/api/analyses", new { jobPosting = posting });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(0, embeddings.CallCount);
    }
}

public class AnalysisRoutingTests
{
    [Theory]
    [InlineData(75, 70, true)]
    [InlineData(70, 70, true)]
    [InlineData(69, 70, false)]
    [InlineData(60, 50, true)]
    public void Strong_fit_starts_at_the_threshold(int score, int threshold, bool strong) =>
        Assert.Equal(strong, JobAnalysisWorkflow.IsStrongFit(score, threshold));
}

public class StudyPlanGroundingTests
{
    private static readonly RequirementAssessment[] Assessments =
    [
        Assessment("req-met", RequirementImportance.MustHave, FitStatus.Met),
        Assessment("req-nice", RequirementImportance.NiceToHave, FitStatus.Missing),
        Assessment("req-must", RequirementImportance.MustHave, FitStatus.Partial),
    ];

    [Fact]
    public void Items_for_met_or_unknown_requirements_are_dropped()
    {
        var plan = StudyPlanner.Ground(Assessments, [Item("req-nice"), Item("req-met"), Item("req-unknown")]);

        Assert.Equal("req-nice", Assert.Single(plan).RequirementId);
    }

    [Fact]
    public void Must_have_items_come_first_and_duplicates_are_removed()
    {
        var plan = StudyPlanner.Ground(Assessments, [Item("req-nice"), Item("req-must", "first"), Item("req-must", "second")]);

        Assert.Equal(["req-must", "req-nice"], plan.Select(i => i.RequirementId));
        Assert.Equal("first", plan[0].Focus);
    }

    [Fact]
    public void Items_without_focus_are_dropped_and_blank_steps_removed()
    {
        var plan = StudyPlanner.Ground(
            Assessments, [new StudyItemOutput("req-nice", " ", ["x"]), new StudyItemOutput("req-must", "Learn it", ["Do it", " "])]);

        var item = Assert.Single(plan);
        Assert.Equal("req-must", item.RequirementId);
        Assert.Equal(["Do it"], item.Steps);
    }

    private static RequirementAssessment Assessment(string id, RequirementImportance importance, FitStatus status) =>
        new(id, id, importance, status, "rationale", []);

    private static StudyItemOutput Item(string id, string focus = "Learn it") => new(id, focus, ["Take a course"]);
}
