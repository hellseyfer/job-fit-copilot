using Npgsql;

namespace JobFit.Api.Analysis;

// Runs a CREATE TABLE IF NOT EXISTS script once per process, on first use; a failed attempt is retried next time.
public sealed class SchemaInitializer(NpgsqlDataSource dataSource, string ddl)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private volatile bool _created;

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        if (_created)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_created)
            {
                await using var command = dataSource.CreateCommand(ddl);
                await command.ExecuteNonQueryAsync(cancellationToken);
                _created = true;
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}
