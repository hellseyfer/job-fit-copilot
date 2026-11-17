using System.ClientModel;
using JobFit.Api.Knowledge;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace JobFit.Evals;

[CollectionDefinition("Evals", DisableParallelization = true)]
public sealed class EvalCollection;

internal static class EvalHarness
{
    private const string MissingModelMessage =
        "Set OPENAI_API_KEY or OLLAMA_ENDPOINT to run the evals.";

    private const string RequiredModelMessage =
        "A real model is required because JOBFIT_EVAL_REQUIRED=true. Set OPENAI_API_KEY or OLLAMA_ENDPOINT.";

    internal static IChatClient RequireChatClient(Func<string, string?>? getEnv = null)
    {
        getEnv ??= Environment.GetEnvironmentVariable;
        var client = CreateChatClient(getEnv);
        if (client is not null)
        {
            return client;
        }

        FailOrSkip(getEnv);
        throw new InvalidOperationException(RequiredModelMessage);
    }

    internal static IEmbeddingGenerator<string, Embedding<float>> RequireEmbeddingGenerator(
        Func<string, string?>? getEnv = null)
    {
        getEnv ??= Environment.GetEnvironmentVariable;
        var generator = CreateEmbeddingGenerator(getEnv);
        if (generator is not null)
        {
            return generator;
        }

        FailOrSkip(getEnv);
        throw new InvalidOperationException(RequiredModelMessage);
    }

    internal static string ChatModelName(Func<string, string?>? getEnv = null)
    {
        getEnv ??= Environment.GetEnvironmentVariable;
        var model = getEnv("JOBFIT_EVAL_MODEL");
        if (!string.IsNullOrEmpty(model))
        {
            return model;
        }

        return !string.IsNullOrEmpty(getEnv("OPENAI_API_KEY")) ? "gpt-4o-mini" : "qwen2.5:3b";
    }

    internal static string EmbeddingModelName(Func<string, string?>? getEnv = null)
    {
        getEnv ??= Environment.GetEnvironmentVariable;
        var model = getEnv("JOBFIT_EVAL_EMBEDDING_MODEL");
        if (!string.IsNullOrEmpty(model))
        {
            return model;
        }

        return !string.IsNullOrEmpty(getEnv("OPENAI_API_KEY")) ? "text-embedding-3-small" : "nomic-embed-text";
    }

    internal static IChatClient? CreateChatClient(Func<string, string?>? getEnv = null)
    {
        getEnv ??= Environment.GetEnvironmentVariable;
        var model = getEnv("JOBFIT_EVAL_MODEL");

        var openAiKey = getEnv("OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(openAiKey))
        {
            return new OpenAIClient(new ApiKeyCredential(openAiKey))
                .GetChatClient(string.IsNullOrEmpty(model) ? "gpt-4o-mini" : model)
                .AsIChatClient();
        }

        var ollamaEndpoint = getEnv("OLLAMA_ENDPOINT");
        if (!string.IsNullOrEmpty(ollamaEndpoint))
        {
            var httpClient = new HttpClient { BaseAddress = new Uri(ollamaEndpoint), Timeout = TimeSpan.FromMinutes(10) };
            return new OllamaApiClient(httpClient, string.IsNullOrEmpty(model) ? "qwen2.5:3b" : model);
        }

        return null;
    }

    internal static IEmbeddingGenerator<string, Embedding<float>>? CreateEmbeddingGenerator(
        Func<string, string?>? getEnv = null)
    {
        getEnv ??= Environment.GetEnvironmentVariable;
        var model = getEnv("JOBFIT_EVAL_EMBEDDING_MODEL");

        var openAiKey = getEnv("OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(openAiKey))
        {
            return new OpenAIClient(new ApiKeyCredential(openAiKey))
                .GetEmbeddingClient(string.IsNullOrEmpty(model) ? "text-embedding-3-small" : model)
                .AsIEmbeddingGenerator()
                .AsBuilder()
                // Same as the API: text-embedding-3 models shorten their vectors to the store's fixed size.
                .ConfigureOptions(options => options.Dimensions ??= KnowledgeChunkRecord.Dimensions)
                .Build();
        }

        var ollamaEndpoint = getEnv("OLLAMA_ENDPOINT");
        if (!string.IsNullOrEmpty(ollamaEndpoint))
        {
            var httpClient = new HttpClient { BaseAddress = new Uri(ollamaEndpoint), Timeout = TimeSpan.FromMinutes(10) };
            return new OllamaApiClient(httpClient, string.IsNullOrEmpty(model) ? "nomic-embed-text" : model);
        }

        return null;
    }

    private static void FailOrSkip(Func<string, string?> getEnv)
    {
        if (string.Equals(getEnv("JOBFIT_EVAL_REQUIRED"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail(RequiredModelMessage);
        }

        Skip.If(true, MissingModelMessage);
    }
}
