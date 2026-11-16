using System.Text.Json;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.Checkpointing;
using Npgsql;
using NpgsqlTypes;

namespace JobFit.Api.Analysis;

// Agent Framework checkpoints as opaque JSON documents keyed by workflow session.
// The column is json, not jsonb: jsonb reorders keys and the checkpoint relies on System.Text.Json metadata order.
public sealed class PostgresCheckpointStore(NpgsqlDataSource dataSource) : JsonCheckpointStore
{
    private readonly SchemaInitializer _schema = new(dataSource, """
        CREATE TABLE IF NOT EXISTS workflow_checkpoints (
            session_id text NOT NULL,
            checkpoint_id text NOT NULL,
            parent_id text NULL,
            data json NOT NULL,
            created_at timestamptz NOT NULL DEFAULT now(),
            PRIMARY KEY (session_id, checkpoint_id));
        """);

    public override async ValueTask<CheckpointInfo> CreateCheckpointAsync(
        string sessionId, JsonElement value, CheckpointInfo? parent = null)
    {
        await _schema.EnsureCreatedAsync(CancellationToken.None);
        var checkpoint = new CheckpointInfo(sessionId, Guid.NewGuid().ToString("N"));
        await using var command = dataSource.CreateCommand("""
            INSERT INTO workflow_checkpoints (session_id, checkpoint_id, parent_id, data)
            VALUES (@session, @checkpoint, @parent, @data)
            """);
        command.Parameters.AddWithValue("session", sessionId);
        command.Parameters.AddWithValue("checkpoint", checkpoint.CheckpointId);
        command.Parameters.AddWithValue("parent", (object?)parent?.CheckpointId ?? DBNull.Value);
        command.Parameters.AddWithValue("data", NpgsqlDbType.Json, value.GetRawText());
        await command.ExecuteNonQueryAsync();
        return checkpoint;
    }

    public override async ValueTask<JsonElement> RetrieveCheckpointAsync(string sessionId, CheckpointInfo key)
    {
        await _schema.EnsureCreatedAsync(CancellationToken.None);
        await using var command = dataSource.CreateCommand(
            "SELECT data::text FROM workflow_checkpoints WHERE session_id = @session AND checkpoint_id = @checkpoint");
        command.Parameters.AddWithValue("session", sessionId);
        command.Parameters.AddWithValue("checkpoint", key.CheckpointId);
        var data = await command.ExecuteScalarAsync() as string
                   ?? throw new KeyNotFoundException($"Checkpoint '{key.CheckpointId}' of session '{sessionId}' does not exist.");
        using var document = JsonDocument.Parse(data);
        return document.RootElement.Clone();
    }

    public override async ValueTask<IEnumerable<CheckpointInfo>> RetrieveIndexAsync(
        string sessionId, CheckpointInfo? withParent = null)
    {
        await _schema.EnsureCreatedAsync(CancellationToken.None);
        await using var command = dataSource.CreateCommand("""
            SELECT checkpoint_id FROM workflow_checkpoints
            WHERE session_id = @session AND (@parent::text IS NULL OR parent_id = @parent)
            ORDER BY created_at
            """);
        command.Parameters.AddWithValue("session", sessionId);
        command.Parameters.AddWithValue("parent", NpgsqlDbType.Text, (object?)withParent?.CheckpointId ?? DBNull.Value);
        var checkpoints = new List<CheckpointInfo>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            checkpoints.Add(new CheckpointInfo(sessionId, reader.GetString(0)));
        }

        return checkpoints;
    }
}
