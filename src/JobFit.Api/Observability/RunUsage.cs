using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;

namespace JobFit.Api.Observability;

public sealed record RunUsage(
    long InputTokens,
    long OutputTokens,
    long TotalTokens,
    double CostUsd,
    double DurationMs,
    int ModelCalls)
{
    public static RunUsage Empty { get; } = new(0, 0, 0, 0, 0, 0);

    public static RunUsage Combine(RunUsage? previous, RunUsage current) =>
        previous is null
            ? current
            : new RunUsage(
                previous.InputTokens + current.InputTokens,
                previous.OutputTokens + current.OutputTokens,
                previous.TotalTokens + current.TotalTokens,
                Math.Round(previous.CostUsd + current.CostUsd, 8, MidpointRounding.AwayFromZero),
                Math.Round(previous.DurationMs + current.DurationMs, 2, MidpointRounding.AwayFromZero),
                previous.ModelCalls + current.ModelCalls);
}

public sealed class UsageScope
{
    private readonly Lock _sync = new();
    private long _inputTokens;
    private long _outputTokens;
    private long _totalTokens;
    private double _costUsd;
    private int _modelCalls;
    private readonly long _startedAt = Stopwatch.GetTimestamp();

    public void Add(long inputTokens, long outputTokens, long totalTokens, double costUsd)
    {
        lock (_sync)
        {
            _inputTokens += inputTokens;
            _outputTokens += outputTokens;
            _totalTokens += totalTokens;
            _costUsd += costUsd;
            _modelCalls++;
        }
    }

    public RunUsage Snapshot()
    {
        lock (_sync)
        {
            var elapsedMs = Math.Round(Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds, 2, MidpointRounding.AwayFromZero);
            return new RunUsage(
                _inputTokens,
                _outputTokens,
                _totalTokens,
                Math.Round(_costUsd, 8, MidpointRounding.AwayFromZero),
                elapsedMs,
                _modelCalls);
        }
    }
}

public sealed class UsageTracker(IOptions<ObservabilityOptions> options)
{
    public const string TokensCounterName = "jobfit.analysis.tokens";
    public const string CostCounterName = "jobfit.analysis.cost.usd";
    public const string DurationHistogramName = "jobfit.analysis.duration.ms";

    private static readonly Meter Meter = new(Microsoft.Extensions.Hosting.Extensions.ObservabilityMeter);
    private static readonly Counter<long> TokensCounter = Meter.CreateCounter<long>(TokensCounterName);
    private static readonly Counter<double> CostCounter = Meter.CreateCounter<double>(CostCounterName);
    private static readonly Histogram<double> DurationHistogram = Meter.CreateHistogram<double>(DurationHistogramName);

    private static readonly AsyncLocal<UsageScope?> CurrentScope = new();

    public ObservabilityOptions Options => options.Value;

    public ScopeHandle BeginScope()
    {
        var previous = CurrentScope.Value;
        var scope = new UsageScope();
        CurrentScope.Value = scope;
        return new ScopeHandle(scope, previous);
    }

    public ScopeHandle BeginScopeIfNone()
    {
        if (CurrentScope.Value is { } existing)
        {
            return new ScopeHandle(existing, existing, restore: false);
        }

        return BeginScope();
    }

    public RunUsage CurrentSnapshot() => CurrentScope.Value?.Snapshot() ?? RunUsage.Empty;

    public void RecordCall(string? modelId, long inputTokens, long outputTokens, long totalTokens)
    {
        var model = string.IsNullOrWhiteSpace(modelId) ? "unknown" : modelId;
        var effectiveTotal = totalTokens > 0 ? totalTokens : inputTokens + outputTokens;
        var costUsd = CostCalculator.Calculate(model, inputTokens, outputTokens, options.Value);

        if (inputTokens > 0)
        {
            TokensCounter.Add(inputTokens, new KeyValuePair<string, object?>("model", model), new KeyValuePair<string, object?>("kind", "input"));
        }

        if (outputTokens > 0)
        {
            TokensCounter.Add(outputTokens, new KeyValuePair<string, object?>("model", model), new KeyValuePair<string, object?>("kind", "output"));
        }

        if (costUsd > 0)
        {
            CostCounter.Add(costUsd, new KeyValuePair<string, object?>("model", model));
        }

        CurrentScope.Value?.Add(inputTokens, outputTokens, effectiveTotal, costUsd);
    }

    public static void RecordRunCompleted(RunUsage usage, string route, string status)
    {
        DurationHistogram.Record(
            usage.DurationMs,
            new KeyValuePair<string, object?>("route", route),
            new KeyValuePair<string, object?>("status", status));

        var activity = Activity.Current;
        if (activity is not null)
        {
            activity.SetTag("jobfit.usage.input_tokens", usage.InputTokens);
            activity.SetTag("jobfit.usage.output_tokens", usage.OutputTokens);
            activity.SetTag("jobfit.usage.total_tokens", usage.TotalTokens);
            activity.SetTag("jobfit.usage.cost_usd", usage.CostUsd);
            activity.SetTag("jobfit.usage.duration_ms", usage.DurationMs);
            activity.SetTag("jobfit.usage.model_calls", usage.ModelCalls);
        }
    }

    public sealed class ScopeHandle(UsageScope scope, UsageScope? previous, bool restore = true) : IDisposable
    {
        public UsageScope Scope => scope;

        public void Dispose()
        {
            if (restore)
            {
                CurrentScope.Value = previous;
            }
        }
    }
}
