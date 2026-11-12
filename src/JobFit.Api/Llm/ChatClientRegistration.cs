using Microsoft.Extensions.AI;
using Microsoft.Extensions.Http.Resilience;

namespace JobFit.Api.Llm;

public static class ChatClientRegistration
{
    public const string ConnectionName = "chat";

    public static IHostApplicationBuilder AddJobFitChatClient(this IHostApplicationBuilder builder)
    {
        var provider = builder.Configuration["Llm:Provider"] ?? "openai";

        // Provider tracing is disabled so every provider gets the same telemetry from the shared pipeline.
        var chatClient = provider switch
        {
            "openai" => builder.AddOpenAIClient(ConnectionName, settings => settings.DisableTracing = true)
                .AddChatClient(),
            "ollama" => builder.AddOllamaApiClient(ConnectionName, settings => settings.DisableTracing = true)
                .AddChatClient(),
            _ => throw new InvalidOperationException(
                $"Unsupported Llm:Provider '{provider}'. Use 'openai' or 'ollama'."),
        };

        chatClient.UseJobFitPipeline();

        // Model calls take far longer than the standard resilience timeouts (10 s per attempt), above all
        // on a local CPU model. The API makes no other HTTP calls, so the defaults apply to all clients.
        builder.Services.ConfigureAll<HttpStandardResilienceOptions>(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(3);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(6);
        });

        return builder;
    }

    public static ChatClientBuilder UseJobFitPipeline(this ChatClientBuilder builder) =>
        builder
            .UseOpenTelemetry()
            .UseLogging();
}
