using System.Diagnostics.Metrics;
using JobFit.Api.Llm;
using JobFit.Api.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

namespace JobFit.Api.Tests;

[Collection(TelemetryCollection.Name)]
public class ObservabilityTests
{
    [Fact]
    public void CostCalculator_uses_default_and_overridden_pricing_and_zero_for_unknown_models()
    {
        var defaultCost = CostCalculator.Calculate("gpt-4o-mini", inputTokens: 1_000_000, outputTokens: 500_000);
        Assert.Equal(0.45, defaultCost);
        Assert.Equal(0.45, CostCalculator.Calculate("gpt-4o-mini-2024-07-18", 1_000_000, 500_000));

        var options = new ObservabilityOptions
        {
            ModelPricing = new Dictionary<string, ModelPrice>
            {
                ["gpt-4o-mini"] = new ModelPrice { InputPerMillionUsd = 1.0, OutputPerMillionUsd = 2.0 },
                ["custom-model"] = new ModelPrice { InputPerMillionUsd = 0.5, OutputPerMillionUsd = 1.5 },
            },
        };

        Assert.Equal(2.0, CostCalculator.Calculate("gpt-4o-mini", 1_000_000, 500_000, options));
        Assert.Equal(1.25, CostCalculator.Calculate("custom-model", 1_000_000, 500_000, options));
        Assert.Equal(0, CostCalculator.Calculate("qwen2.5:3b", 10_000, 5_000, options));
        Assert.Equal(0, CostCalculator.Calculate(null, 10_000, 5_000, options));
    }

    [Fact]
    public async Task Pipeline_aggregates_tokens_cost_and_emits_observability_metrics()
    {
        var recordedTokens = new List<(long Value, string? Model, string? Kind)>();
        var recordedCosts = new List<(double Value, string? Model)>();
        var recordedDurations = new List<(double Value, string? Route, string? Status)>();

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == Extensions.ObservabilityMeter)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == UsageTracker.TokensCounterName)
            {
                string? model = null;
                string? kind = null;
                foreach (var tag in tags)
                {
                    if (tag.Key == "model") model = tag.Value as string;
                    if (tag.Key == "kind") kind = tag.Value as string;
                }
                recordedTokens.Add((measurement, model, kind));
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
        {
            if (instrument.Name == UsageTracker.CostCounterName)
            {
                string? model = null;
                foreach (var tag in tags)
                {
                    if (tag.Key == "model") model = tag.Value as string;
                }
                recordedCosts.Add((measurement, model));
            }
            else if (instrument.Name == UsageTracker.DurationHistogramName)
            {
                string? route = null;
                string? status = null;
                foreach (var tag in tags)
                {
                    if (tag.Key == "route") route = tag.Value as string;
                    if (tag.Key == "status") status = tag.Value as string;
                }
                recordedDurations.Add((measurement, route, status));
            }
        });
        listener.Start();

        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(new ObservabilityOptions
        {
            ModelPricing = new Dictionary<string, ModelPrice>
            {
                [FakeChatClient.ModelId] = new ModelPrice { InputPerMillionUsd = 10, OutputPerMillionUsd = 20 },
                [FakeEmbeddingGenerator.ModelId] = new ModelPrice { InputPerMillionUsd = 1, OutputPerMillionUsd = 0 },
            },
        }));
        services.AddSingleton<UsageTracker>();
        services.AddChatClient(FakeChatClient.Returning("{}")).UseJobFitPipeline();
        services.AddEmbeddingGenerator(new FakeEmbeddingGenerator()).UseJobFitPipeline();

        await using var provider = services.BuildServiceProvider();
        var tracker = provider.GetRequiredService<UsageTracker>();
        var chat = provider.GetRequiredService<IChatClient>();
        var embeddings = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        using var scope = tracker.BeginScope();
        await chat.GetResponseAsync("hello");
        await embeddings.GenerateAsync(["one two three"]);
        var usage = scope.Scope.Snapshot();
        UsageTracker.RecordRunCompleted(usage, "StrongFit", "AwaitingApproval");

        Assert.Equal(123, usage.InputTokens); // 120 chat + 3 embedding words
        Assert.Equal(45, usage.OutputTokens);
        Assert.Equal(168, usage.TotalTokens);
        Assert.Equal(2, usage.ModelCalls);
        Assert.True(usage.DurationMs >= 0);
        // chat: (120*10 + 45*20)/1e6 = 0.0021; embeddings: (3*1)/1e6 = 0.000003
        Assert.Equal(0.002103, usage.CostUsd);

        Assert.Contains(recordedTokens, t => t is { Model: FakeChatClient.ModelId, Kind: "input", Value: 120 });
        Assert.Contains(recordedTokens, t => t is { Model: FakeChatClient.ModelId, Kind: "output", Value: 45 });
        Assert.Contains(recordedTokens, t => t is { Model: FakeEmbeddingGenerator.ModelId, Kind: "input", Value: 3 });
        Assert.Contains(recordedCosts, c => c.Model == FakeChatClient.ModelId && Math.Abs(c.Value - 0.0021) < 1e-9);
        Assert.Contains(recordedDurations, d => d is { Route: "StrongFit", Status: "AwaitingApproval" } && d.Value >= 0);
    }

    [Fact]
    public void Langfuse_otlp_exporter_is_registered_only_when_credentials_are_configured()
    {
        var unconfigured = WebApplication.CreateBuilder();
        unconfigured.AddServiceDefaults();
        using var unconfiguredProvider = unconfigured.Services.BuildServiceProvider();
        var unconfiguredOptions = unconfiguredProvider.GetServices<IConfigureOptions<OtlpExporterOptions>>().ToList();
        Assert.Empty(unconfiguredOptions);

        var configured = WebApplication.CreateBuilder();
        configured.Configuration["Observability:Langfuse:PublicKey"] = "pk-lf-test";
        configured.Configuration["Observability:Langfuse:SecretKey"] = "sk-lf-test";
        configured.Configuration["Observability:Langfuse:Endpoint"] = "https://langfuse.example.com/api/public/otel";
        configured.AddServiceDefaults();
        using var configuredProvider = configured.Services.BuildServiceProvider();
        var langfuseOptions = configuredProvider.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get("langfuse");

        Assert.Equal(new Uri("https://langfuse.example.com/api/public/otel/v1/traces"), langfuseOptions.Endpoint);
        Assert.Equal(OtlpExportProtocol.HttpProtobuf, langfuseOptions.Protocol);
        Assert.Contains("Authorization=Basic ", langfuseOptions.Headers);
    }
}
