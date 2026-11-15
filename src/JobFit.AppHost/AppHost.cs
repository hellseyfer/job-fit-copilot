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
var embeddingModel = builder.Configuration["Llm:EmbeddingModel"];

switch (llmProvider)
{
    case "openai":
        var openAiApiKey = builder.AddParameter("openai-api-key", secret: true);
        var openAi = builder.AddOpenAI("openai").WithApiKey(openAiApiKey);
        var openAiChat = openAi.AddModel("chat", string.IsNullOrEmpty(llmModel) ? "gpt-4o-mini" : llmModel);
        var openAiEmbeddings = openAi.AddModel(
            "embeddings", string.IsNullOrEmpty(embeddingModel) ? "text-embedding-3-small" : embeddingModel);
        api.WithReference(openAiChat).WithReference(openAiEmbeddings);
        break;

    case "ollama":
        var ollama = builder.AddOllama("ollama").WithDataVolume();
        var ollamaChat = ollama.AddModel("chat", string.IsNullOrEmpty(llmModel) ? "qwen2.5:3b" : llmModel);
        var ollamaEmbeddings = ollama.AddModel(
            "embeddings", string.IsNullOrEmpty(embeddingModel) ? "nomic-embed-text" : embeddingModel);
        api.WithReference(ollamaChat).WaitFor(ollamaChat)
            .WithReference(ollamaEmbeddings).WaitFor(ollamaEmbeddings);
        break;

    default:
        throw new InvalidOperationException($"Unsupported Llm:Provider '{llmProvider}'. Use 'openai' or 'ollama'.");
}

api.WithEnvironment("Llm__Provider", llmProvider);

builder.Build().Run();
