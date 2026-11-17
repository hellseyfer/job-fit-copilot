using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    // Default ActivitySource of Microsoft.Extensions.AI's OpenTelemetryChatClient.
    // Microsoft Agent Framework workflow runs and executors.
    public const string WorkflowTraceSource = "Microsoft.Agents.AI.Workflows";

    // Prompt-injection blocks counted by the API guardrails.
    public const string GuardrailsMeter = "JobFit.Guardrails";

    // Per-run token, cost and latency metrics.
    public const string ObservabilityMeter = "JobFit.Observability";

    private const string GenAiTraceSource = "Experimental.Microsoft.Extensions.AI";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(GuardrailsMeter)
                    .AddMeter(ObservabilityMeter);
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddSource(GenAiTraceSource)
                    .AddSource(WorkflowTraceSource)
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        var langfusePublicKey = builder.Configuration["Observability:Langfuse:PublicKey"];
        var langfuseSecretKey = builder.Configuration["Observability:Langfuse:SecretKey"];
        if (!string.IsNullOrWhiteSpace(langfusePublicKey) && !string.IsNullOrWhiteSpace(langfuseSecretKey))
        {
            var langfuseBase = builder.Configuration["Observability:Langfuse:Endpoint"];
            var baseUri = string.IsNullOrWhiteSpace(langfuseBase)
                ? "https://cloud.langfuse.com/api/public/otel"
                : langfuseBase.TrimEnd('/');
            var tracesUri = new Uri($"{baseUri}/v1/traces");
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{langfusePublicKey}:{langfuseSecretKey}"));

            builder.Services.AddOpenTelemetry().WithTracing(tracing =>
                tracing.AddOtlpExporter("langfuse", options =>
                {
                    options.Endpoint = tracesUri;
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                    options.Headers = $"Authorization=Basic {auth}";
                }));
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Health endpoints only report status ("Healthy"/"Unhealthy"), so they are safe to expose
        // in every environment and are needed by container orchestrators.
        // Readiness: all health checks (including the database) must pass.
        app.MapHealthChecks(HealthEndpointPath);

        // Liveness: only checks tagged "live" (the process itself) must pass.
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }
}
