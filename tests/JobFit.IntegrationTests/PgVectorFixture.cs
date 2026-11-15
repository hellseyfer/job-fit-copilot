using Testcontainers.PostgreSql;

namespace JobFit.IntegrationTests;

// One pgvector container shared by the knowledge tests; each test uses its own document ids.
public sealed class PgVectorFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("jobfit")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class PgVectorCollection : ICollectionFixture<PgVectorFixture>
{
    public const string Name = "pgvector";
}
