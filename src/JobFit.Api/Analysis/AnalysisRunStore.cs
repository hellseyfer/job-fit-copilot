using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace JobFit.Api.Analysis;

public sealed record ClaimedRun(string CheckpointId, AnalyzeJobResponse Response);

// One row per analysis: its status, the response so far and the checkpoint a paused run resumes from.
public sealed class AnalysisRunStore(NpgsqlDataSource dataSource, IOptions<JsonOptions> jsonOptions)
{
    private readonly JsonSerializerOptions _json = jsonOptions.Value.SerializerOptions;

    private readonly SchemaInitializer _schema = new(dataSource, """
        CREATE TABLE IF NOT EXISTS analysis_runs (
            run_id text PRIMARY KEY,
            checkpoint_id text NULL,
            request jsonb NOT NULL,
            status text NOT NULL,
            result jsonb NOT NULL,
            feedback_rounds integer NOT NULL DEFAULT 0,
            updated_at timestamptz NOT NULL DEFAULT now());
        """);

    public async Task CreateAsync(
        AnalysisInput request, AnalyzeJobResponse response, string? checkpointId, int feedbackRounds, CancellationToken cancellationToken)
    {
        await _schema.EnsureCreatedAsync(cancellationToken);
        await using var command = dataSource.CreateCommand("""
            INSERT INTO analysis_runs (run_id, checkpoint_id, request, status, result, feedback_rounds)
            VALUES (@id, @checkpoint, @request, @status, @result, @rounds)
            """);
        command.Parameters.AddWithValue("request", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(request, _json));
        AddState(command, response, checkpointId, feedbackRounds);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<AnalyzeJobResponse?> GetAsync(string runId, CancellationToken cancellationToken)
    {
        await _schema.EnsureCreatedAsync(cancellationToken);
        await using var command = dataSource.CreateCommand("SELECT status, result::text FROM analysis_runs WHERE run_id = @id");
        command.Parameters.AddWithValue("id", runId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader.GetString(0), reader.GetString(1)) : null;
    }

    // Atomically moves a paused run to resuming; null when the run is not awaiting approval (or does not exist).
    public async Task<ClaimedRun?> ClaimAsync(string runId, CancellationToken cancellationToken)
    {
        await _schema.EnsureCreatedAsync(cancellationToken);
        await using var command = dataSource.CreateCommand("""
            UPDATE analysis_runs SET status = @resuming, updated_at = now()
            WHERE run_id = @id AND status = @awaiting
            RETURNING checkpoint_id, result::text
            """);
        command.Parameters.AddWithValue("id", runId);
        command.Parameters.AddWithValue("resuming", Name(AnalysisStatus.Resuming));
        command.Parameters.AddWithValue("awaiting", Name(AnalysisStatus.AwaitingApproval));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ClaimedRun(reader.GetString(0), Read(Name(AnalysisStatus.Resuming), reader.GetString(1)))
            : null;
    }

    public async Task UpdateAsync(
        AnalyzeJobResponse response, string? checkpointId, int feedbackRounds, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("""
            UPDATE analysis_runs
            SET status = @status, result = @result, checkpoint_id = @checkpoint, feedback_rounds = @rounds, updated_at = now()
            WHERE run_id = @id
            """);
        AddState(command, response, checkpointId, feedbackRounds);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Gives a claimed run back to the reviewer after a failed resume.
    public async Task ReleaseAsync(string runId, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "UPDATE analysis_runs SET status = @awaiting, updated_at = now() WHERE run_id = @id AND status = @resuming");
        command.Parameters.AddWithValue("id", runId);
        command.Parameters.AddWithValue("resuming", Name(AnalysisStatus.Resuming));
        command.Parameters.AddWithValue("awaiting", Name(AnalysisStatus.AwaitingApproval));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private void AddState(NpgsqlCommand command, AnalyzeJobResponse response, string? checkpointId, int feedbackRounds)
    {
        command.Parameters.AddWithValue("id", response.RunId);
        command.Parameters.AddWithValue("checkpoint", (object?)checkpointId ?? DBNull.Value);
        command.Parameters.AddWithValue("status", Name(response.Status));
        command.Parameters.AddWithValue("result", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(response, _json));
        command.Parameters.AddWithValue("rounds", feedbackRounds);
    }

    private AnalyzeJobResponse Read(string status, string result) =>
        JsonSerializer.Deserialize<AnalyzeJobResponse>(result, _json)! with
        {
            Status = Enum.Parse<AnalysisStatus>(status.Replace("-", string.Empty), ignoreCase: true),
        };

    private static string Name(AnalysisStatus status) => JsonNamingPolicy.KebabCaseLower.ConvertName(status.ToString());
}
