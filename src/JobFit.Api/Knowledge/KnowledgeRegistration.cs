using Microsoft.Extensions.DependencyInjection;

namespace JobFit.Api.Knowledge;

public static class KnowledgeRegistration
{
    public static IHostApplicationBuilder AddKnowledgeIngestion(this IHostApplicationBuilder builder)
    {
        // The collection uses the Aspire NpgsqlDataSource, which maps pgvector types (UseVector in Program).
        builder.Services.AddPostgresCollection<string, KnowledgeChunkRecord>(KnowledgeChunkRecord.CollectionName);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<KnowledgeIngestionService>();
        return builder;
    }
}
