var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg17")
    .WithDataVolume();

var database = postgres.AddDatabase("jobfit");

var api = builder.AddProject<Projects.JobFit_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health", endpointName: "http");

var llmProvider = builder.Configuration["Llm:Provider"] ?? "openai";
var llmModel = builder.Configuration["Llm:Model"];

switch (llmProvider)
{
    case "openai":
        var openAiApiKey = builder.AddParameter("openai-api-key", secret: true);
        var openAiModel = builder.AddOpenAI("openai")
            .WithApiKey(openAiApiKey)
            .AddModel("chat", string.IsNullOrEmpty(llmModel) ? "gpt-4o-mini" : llmModel);
        api.WithReference(openAiModel);
        break;

    case "ollama":
        var ollamaModel = builder.AddOllama("ollama")
            .WithDataVolume()
            .AddModel("chat", string.IsNullOrEmpty(llmModel) ? "qwen2.5:3b" : llmModel);
        api.WithReference(ollamaModel).WaitFor(ollamaModel);
        break;

    default:
        throw new InvalidOperationException($"Unsupported Llm:Provider '{llmProvider}'. Use 'openai' or 'ollama'.");
}

api.WithEnvironment("Llm__Provider", llmProvider);

builder.Build().Run();
