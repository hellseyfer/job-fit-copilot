using JobFit.Api.Llm;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace JobFit.Api.Tests;

// In-memory API with an unreachable database and, optionally, a fake model behind the real chat pipeline.
public class JobFitApiFactory(FakeChatClient? chatClient = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting(
            "ConnectionStrings:jobfit",
            "Host=127.0.0.1;Port=1;Database=jobfit;Username=jobfit;Password=unused;Timeout=2");
        builder.UseSetting("Llm:Provider", "openai");
        builder.UseSetting(
            "ConnectionStrings:chat",
            "Endpoint=http://127.0.0.1:1/v1;Key=unused;Model=unused");

        if (chatClient is not null)
        {
            builder.ConfigureTestServices(services => services.AddChatClient(chatClient).UseJobFitPipeline());
        }
    }
}
