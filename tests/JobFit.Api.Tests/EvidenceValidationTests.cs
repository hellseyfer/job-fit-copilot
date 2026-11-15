using System.Net;
using System.Net.Http.Json;

namespace JobFit.Api.Tests;

// Invalid searches are rejected before the database or the embedding model are used.
public class EvidenceValidationTests
{
    private static readonly object Valid = new { id = "req-1", description = "Experience with PostgreSQL" };

    [Fact]
    public async Task Empty_requirements_return_400_without_embedding()
    {
        var (status, embeddings) = await SearchAsync(new { requirements = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(0, embeddings.CallCount);
    }

    [Fact]
    public async Task Missing_requirements_return_400_without_embedding()
    {
        var (status, embeddings) = await SearchAsync(new { top = 3 });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(0, embeddings.CallCount);
    }

    [Fact]
    public async Task Too_many_requirements_return_400_without_embedding()
    {
        var requirements = Enumerable.Range(1, 51).Select(i => new { id = $"req-{i}", description = "C#" });

        var (status, embeddings) = await SearchAsync(new { requirements });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(0, embeddings.CallCount);
    }

    [Fact]
    public async Task Missing_id_returns_400_without_embedding()
    {
        var (status, embeddings) = await SearchAsync(new { requirements = new object[] { new { description = "C#" } } });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(0, embeddings.CallCount);
    }

    [Fact]
    public async Task Duplicate_ids_return_400_without_embedding()
    {
        var (status, embeddings) = await SearchAsync(new
        {
            requirements = new object[] { Valid, new { id = "req-1", description = "Docker" } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(0, embeddings.CallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Empty_description_returns_400_without_embedding(string? description)
    {
        var (status, embeddings) = await SearchAsync(new { requirements = new object[] { new { id = "req-1", description } } });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(0, embeddings.CallCount);
    }

    [Fact]
    public async Task Oversized_description_returns_400_without_embedding()
    {
        var (status, embeddings) = await SearchAsync(new
        {
            requirements = new object[] { new { id = "req-1", description = new string('a', 1_001) } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(0, embeddings.CallCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task Top_out_of_range_returns_400_without_embedding(int top)
    {
        var (status, embeddings) = await SearchAsync(new { requirements = new[] { Valid }, top });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(0, embeddings.CallCount);
    }

    [Fact]
    public async Task Unknown_kind_returns_400_without_embedding()
    {
        var (status, embeddings) = await SearchAsync(new { requirements = new[] { Valid }, kind = "letter" });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(0, embeddings.CallCount);
    }

    private static async Task<(HttpStatusCode Status, FakeEmbeddingGenerator Embeddings)> SearchAsync(object body)
    {
        var embeddings = new FakeEmbeddingGenerator();
        await using var factory = new JobFitApiFactory(embeddingGenerator: embeddings);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/evidence/search", body);
        return (response.StatusCode, embeddings);
    }
}
