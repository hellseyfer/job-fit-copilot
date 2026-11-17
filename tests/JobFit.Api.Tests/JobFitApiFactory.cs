using JobFit.Api.Llm;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace JobFit.Api.Tests;

// In-memory API with an unreachable database and, optionally, fake models behind the real pipelines.
// The guardrail classifier is off unless a test turns it on: FakeChatClient answers every call with one JSON.
public class JobFitApiFactory(
    FakeChatClient? chatClient = null, FakeEmbeddingGenerator? embeddingGenerator = null, bool classifier = false)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(
            "ConnectionStrings:jobfit",
            "Host=127.0.0.1;Port=1;Database=jobfit;Username=jobfit;Password=unused;Timeout=2");
        builder.UseSetting("Llm:Provider", "openai");
        builder.UseSetting("Guardrails:Classifier", classifier ? "true" : "false");
        builder.UseSetting(
            "ConnectionStrings:chat",
            "Endpoint=http://127.0.0.1:1/v1;Key=unused;Model=unused");
        builder.UseSetting(
            "ConnectionStrings:embeddings",
            "Endpoint=http://127.0.0.1:1/v1;Key=unused;Model=unused");

        if (chatClient is not null)
        {
            builder.ConfigureTestServices(services => services.AddChatClient(chatClient).UseJobFitPipeline());
        }

        if (embeddingGenerator is not null)
        {
            builder.ConfigureTestServices(services =>
                services.AddEmbeddingGenerator(embeddingGenerator).UseJobFitPipeline());
        }
    }
}
