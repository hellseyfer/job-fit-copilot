using System.Net;
using System.Net.Http.Json;

namespace JobFit.Api.Tests;

// Invalid input is rejected before the database or the embedding model are used.
public class KnowledgeValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   \n\t  ")]
    public async Task Empty_content_returns_400_without_embedding(string content)
    {
        var (status, embeddings) = await PutAsync("cv", new { kind = "cv", title = "My CV", content });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(0, embeddings.CallCount);
    }

    [Fact]
    public async Task Oversized_content_returns_400_without_embedding()
    {
        var (status, embeddings) = await PutAsync(
            "cv", new { kind = "cv", title = "My CV", content = new string('a', 50_001) });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(0, embeddings.CallCount);
    }

    [Theory]
    [InlineData("My CV!", "cv")]
    [InlineData("cv", "letter")]
    public async Task Invalid_id_or_kind_returns_400_without_embedding(string id, string kind)
    {
        var (status, embeddings) = await PutAsync(
            Uri.EscapeDataString(id), new { kind, title = "My CV", content = "C# developer." });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(0, embeddings.CallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Missing_title_returns_400_without_embedding(string? title)
    {
        var (status, embeddings) = await PutAsync("cv", new { kind = "cv", title, content = "C# developer." });

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(0, embeddings.CallCount);
    }

    private static async Task<(HttpStatusCode Status, FakeEmbeddingGenerator Embeddings)> PutAsync(string id, object body)
    {
        var embeddings = new FakeEmbeddingGenerator();
        await using var factory = new JobFitApiFactory(embeddingGenerator: embeddings);
        using var client = factory.CreateClient();

        using var response = await client.PutAsJsonAsync($"/api/knowledge/documents/{id}", body);
        return (response.StatusCode, embeddings);
    }
}
