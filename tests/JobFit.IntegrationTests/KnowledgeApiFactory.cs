using JobFit.Api.Knowledge;
using JobFit.Api.Llm;
using JobFit.Api.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;

namespace JobFit.IntegrationTests;

// In-memory API against the real pgvector container, with fake models behind the real pipeline.
public sealed class KnowledgeApiFactory(
    string connectionString, FakeEmbeddingGenerator embeddingGenerator, FakeChatClient? chatClient = null)
    : WebApplicationFactory<KnowledgeIngestionService>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:jobfit", connectionString);
        builder.UseSetting("Llm:Provider", "openai");
        builder.UseSetting("ConnectionStrings:chat", "Endpoint=http://127.0.0.1:1/v1;Key=unused;Model=unused");
        builder.UseSetting("ConnectionStrings:embeddings", "Endpoint=http://127.0.0.1:1/v1;Key=unused;Model=unused");
        builder.ConfigureTestServices(services => services.AddEmbeddingGenerator(embeddingGenerator).UseJobFitPipeline());
        if (chatClient is not null)
        {
            builder.ConfigureTestServices(services => services.AddChatClient(chatClient).UseJobFitPipeline());
        }
    }
}
